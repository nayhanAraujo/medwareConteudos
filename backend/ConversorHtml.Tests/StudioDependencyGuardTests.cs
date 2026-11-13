using System.Text;
using System.Text.Json;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.StudioDrafts;

namespace ConversorHtml.Tests;

public class StudioDependencyGuardTests
{
    private static readonly VariavelOpcao[] Catalog = [
        new(1,"Peso","PESO",null,null,"kg"),new(2,"Altura","ALTURA",null,null,"cm"),
        new(3,"Superfície Corporal","SUPERFCORP","<<VR_PESO>> * <<VR_ALTURA>>",null,"m²",Dependencias:[1,2]),
        new(4,"Volume indexado","INDEX","<<VR_SUPERFCORP>>",null,Dependencias:[3])];
    private static string Id(string name)=>new Guid(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(name))).ToString();
    private static StudioDocument Document() => new("Laudo",[
        new(Id("paciente"),"Dados do paciente",1,[new(Id("peso"),1),new(Id("altura"),2)]),
        new(Id("calculos"),"Cálculos",2,[new(Id("sc"),3),new(Id("index"),4)])]);
    private static DraftCommand Command(string type,object payload)=>new(Guid.NewGuid().ToString(),0,type,JsonSerializer.SerializeToElement(payload));

    [Theory]
    [InlineData("peso","Peso")]
    [InlineData("altura","Altura")]
    public void RequiredMeasureRemovalIsAtomicAndNamesDirectAndIndirectDependents(string id,string name)
    {
        var doc=Document();var original=JsonSerializer.Serialize(doc);
        var failure=Assert.Throws<MeasureInUseException>(()=>StudioDocumentEditor.Apply(doc,Command("deleteItem",new {itemId=Id(id)}),Catalog));
        Assert.Equal("measure_in_use",failure.Code);Assert.Equal(name,Assert.Single(failure.Details.Removed).Nome);
        Assert.Contains(failure.Details.Dependents,d=>d.Measure.Nome=="Superfície Corporal"&&!d.Indirect&&d.Measure.SectionName=="Cálculos");
        Assert.Contains(failure.Details.Dependents,d=>d.Measure.Nome=="Volume indexado"&&d.Indirect);
        Assert.Equal(original,JsonSerializer.Serialize(doc));
    }
    [Theory]
    [InlineData("deleteSection")]
    [InlineData("clearSection")]
    public void WholeSectionOperationChecksConsumersInOtherColumn(string type)
    {
        var doc=Document();var original=JsonSerializer.Serialize(doc);
        Assert.Throws<MeasureInUseException>(()=>StudioDocumentEditor.Apply(doc,Command(type,new {sectionId=Id("paciente")}),Catalog));
        Assert.Equal(original,JsonSerializer.Serialize(doc));
    }
    [Fact]
    public void CalculatedMeasureWithoutConsumersCanBeRemoved()
    {
        var doc=Document();doc.Secoes[1].Itens.RemoveAll(i=>i.Id==Id("index"));
        var result=StudioDocumentEditor.Apply(doc,Command("deleteItem",new {itemId=Id("sc")}),Catalog);
        Assert.Empty(result.Secoes[1].Itens);
    }
    [Fact]
    public void RemovingTheEntireDependentSetIsAllowed()
    {
        var doc=Document();var empty=doc with {Secoes=[]};
        StudioDependencyGuard.Ensure(doc,empty,Catalog,"deleteSection");
    }
    [Fact]
    public void MissingReferencesAlreadyPresentDoNotBlockUnrelatedRemoval()
    {
        var doc=Document();doc.Secoes[0].Itens.RemoveAll(i=>i.Id==Id("peso"));
        var after=StudioDocumentEditor.Copy(doc);after.Secoes[1].Itens.RemoveAll(i=>i.Id==Id("index"));
        StudioDependencyGuard.Ensure(doc,after,Catalog,"deleteItem");
    }
    [Fact]
    public void ImportedHelperUsesActualPreservedFormulaAndDoesNotExecuteIt()
    {
        const string json="""{"camposScript":[{"id":1,"tipo":"etiqueta","nome":"A","descricao":"Paciente","coluna":1},{"id":2,"tipo":"numero","nome":"PESO","descricao":"Peso importado","etiquetaPai":"A","coluna":1},{"id":3,"nome":"AUXILIAR","descricao":"Função auxiliar preservada","coluna":-1,"funcao":"const local = <<VR_PESO>>; window.neverRun = true;"}]}""";
        var doc=StudioJsonImport.Parse(Encoding.UTF8.GetBytes(json),"modelo.json",Catalog);
        var before=StudioJsonImport.Export(doc);
        var failure=Assert.Throws<MeasureInUseException>(()=>StudioDocumentEditor.Apply(doc,Command("deleteItem",new {itemId=doc.Secoes[0].Itens[0].Id}),Catalog));
        Assert.Contains(failure.Details.Dependents,d=>d.Measure.Preserved&&d.Measure.Nome=="Função auxiliar preservada");
        Assert.Equal(before,StudioJsonImport.Export(doc));
    }
    [Fact]
    public void ChangingFormulaToRemoveTheReferenceInSameOperationDoesNotBlockRemoval()
    {
        var weight=new DependencyNode("peso","1",1,"PESO","Peso","s","Paciente",false,[]);
        var calc=new DependencyNode("calc","2",2,"CALC","Cálculo","s","Paciente",false,["peso"]);
        Assert.Null(StudioDependencyGuard.Analyze([weight,calc],[calc with {Dependencies=[]}],"replace"));
    }

    [Fact]
    public void ApiFilterReturnsStructured409InsteadOfGenericFailure()
    {
        var before=Document();var after=StudioDocumentEditor.Copy(before);after.Secoes[0].Itens.RemoveAll(i=>i.CodVariavel==1);
        var block=StudioDependencyGuard.Analyze(StudioDependencyGuard.Graph(before,Catalog),StudioDependencyGuard.Graph(after,Catalog),"deleteItem")!;
        var action=new Microsoft.AspNetCore.Mvc.ActionContext(new Microsoft.AspNetCore.Http.DefaultHttpContext(),new Microsoft.AspNetCore.Routing.RouteData(),new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context=new Microsoft.AspNetCore.Mvc.Filters.ExceptionContext(action,[]) {Exception=new MeasureInUseException(block)};
        new StudioErrors().OnException(context);
        var result=Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(context.Result);
        Assert.True(context.ExceptionHandled);Assert.Equal(409,result.StatusCode);
        var body=JsonSerializer.SerializeToElement(result.Value,StudioDocumentEditor.Json);
        Assert.Equal("measure_in_use",body.GetProperty("code").GetString());
        Assert.Equal("Peso",body.GetProperty("details").GetProperty("removed")[0].GetProperty("nome").GetString());
    }
}
