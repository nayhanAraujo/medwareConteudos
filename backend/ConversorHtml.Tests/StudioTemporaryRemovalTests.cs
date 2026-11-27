using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ConversorHtml.Application.Services;
using ConversorHtml.Domain.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.RealtimeVoice;
using MdwConteudos.Api.Modules.StudioDrafts;

namespace ConversorHtml.Tests;

public class StudioTemporaryRemovalTests
{
    public class CatalogProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method,object?[]? args) => method?.Name=="ListVariaveisAsync"
            ? Task.FromResult<IReadOnlyList<VariavelOpcao>>([]) : throw new InvalidOperationException("Unexpected catalog operation");
    }
    private static (StudioTemporaryCommandsController Controller,VoiceSession Session) Fixture(string currentOwner="1", bool imported = true)
    {
        const string json="""{"camposScript":[{"id":1,"tipo":"etiqueta","nome":"A","descricao":"Paciente","coluna":1},{"id":2,"tipo":"numero","nome":"PESO","descricao":"Peso","etiquetaPai":"A","coluna":1},{"id":3,"tipo":"numero","nome":"SC","descricao":"Superfície Corporal","etiquetaPai":"A","coluna":1,"funcao":"<<VR_PESO>>*2"}]}""";
        var documentJson = JsonNodeWithDestination(json);
        var doc=StudioJsonImport.Parse(Encoding.UTF8.GetBytes(documentJson),"modelo.json",[]);
        var store=new InMemoryVoiceSessionStore();
        var session=store.Create(new VoiceSession {Id=Guid.NewGuid(),OwnerUserId="1",CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow,ExpiresAt=DateTime.UtcNow.AddHours(1),CamposScriptJson=documentJson,ImportedDocumentJson=imported ? JsonSerializer.Serialize(doc,StudioDocumentEditor.Json) : null});
        var manager=new RealtimeVoiceManager(Options.Create(new RealtimeVoiceOptions()),store,null!,null!,null!,NullLoggerFactory.Instance);
        var controller=new StudioTemporaryCommandsController(store,DispatchProxy.Create<IFormulasModelosService,CatalogProxy>(),manager,new StudioChoices(new EphemeralDataProtectionProvider()))
        {ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,currentOwner)],"test"))}}};
        return(controller,session);
    }
    private static string JsonNodeWithDestination(string json)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        root["customRoot"] = "preservado";
        root["camposScript"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse("""{"id":4,"tipo":"etiqueta","nome":"B","descricao":"Cálculos","coluna":2} """));
        return root.ToJsonString();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoveAcrossSectionsPreservesMetadataAndCanBeRetried(bool imported)
    {
        var (controller, session) = Fixture(imported: imported);
        var request = new StudioTemporaryCommandsController.ManualEdit(Guid.NewGuid().ToString(),
            StudioDraftService.Hash(session.ImportedDocumentJson ?? session.CamposScriptJson), "move", ["3"], "4");
        Assert.IsType<OkObjectResult>(await controller.Apply(session.Id, request, default));
        using var output = JsonDocument.Parse(session.CamposScriptJson);
        var moved = output.RootElement.GetProperty("camposScript").EnumerateArray().Single(field => field.GetProperty("id").GetInt32() == 3);
        Assert.Equal("B", moved.GetProperty("etiquetaPai").GetString());
        Assert.Equal(2, moved.GetProperty("coluna").GetInt32());
        Assert.Equal("<<VR_PESO>>*2", moved.GetProperty("funcao").GetString());
        Assert.Equal("preservado", output.RootElement.GetProperty("customRoot").GetString());
        var confirmed = session.CamposScriptJson;
        await controller.Apply(session.Id, request, default);
        Assert.Equal(1, session.ImportedRevision);
        Assert.Equal(confirmed, session.CamposScriptJson);
        if(imported)
        {
            await controller.Apply(session.Id, new(Guid.NewGuid().ToString(), StudioDraftService.Hash(session.ImportedDocumentJson!), "undo", []), default);
            Assert.Equal("A", JsonDocument.Parse(session.CamposScriptJson).RootElement.GetProperty("camposScript").EnumerateArray().Single(field => field.GetProperty("id").GetInt32() == 3).GetProperty("etiquetaPai").GetString());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReorderUsesTheRemainingItemsAndRejectsStaleDestinationAtomically(bool imported)
    {
        var (controller, session) = Fixture(imported: imported);
        await controller.Apply(session.Id, new(Guid.NewGuid().ToString(), StudioDraftService.Hash(session.ImportedDocumentJson ?? session.CamposScriptJson), "move", ["3"], "1", "2"), default);
        var ordered = JsonDocument.Parse(session.CamposScriptJson).RootElement.GetProperty("camposScript").EnumerateArray().Where(field => field.GetProperty("tipo").GetString() == "numero").ToArray();
        Assert.Equal(new[] { 3, 2 }, ordered.OrderBy(field => int.Parse(field.GetProperty("ordem").ToString())).Select(field => field.GetProperty("id").GetInt32()));
        var confirmed = session.CamposScriptJson;
        var failure = await Assert.ThrowsAsync<StudioException>(() => controller.Apply(session.Id,
            new(Guid.NewGuid().ToString(), StudioDraftService.Hash(session.ImportedDocumentJson ?? confirmed), "move", ["3"], "4", "2"), default));
        Assert.Equal(409, failure.Status);
        Assert.Equal(confirmed, session.CamposScriptJson);
        Assert.Equal(1, session.ImportedRevision);
    }
    [Fact]
    public async Task AnotherUserCannotRemoveFieldsOrInspectTheDocument()
    {
        var (controller,session)=Fixture("2");var original=session.ImportedDocumentJson;
        var result=await controller.Apply(session.Id,new(Guid.NewGuid().ToString(),"hash","remove",["unknown"]),default);
        Assert.IsType<NotFoundObjectResult>(result);Assert.Equal(original,session.ImportedDocumentJson);
    }
    [Fact]
    public async Task BlockedRemovalDoesNotAdvanceRevisionOrCreateHistoryOrRetryIdentity()
    {
        var (controller,session)=Fixture();var original=session.ImportedDocumentJson!;
        var id=ImportedVoiceEditor.Document(session).Secoes[0].Itens[0].Id;
        await Assert.ThrowsAsync<MeasureInUseException>(()=>controller.Apply(session.Id,new(Guid.NewGuid().ToString(),StudioDraftService.Hash(original),"remove",[id]),default));
        Assert.Equal(original,session.ImportedDocumentJson);Assert.Equal(0,session.ImportedRevision);
        Assert.Empty(session.ImportedUndo);Assert.Empty(session.ImportedRedo);Assert.Empty(session.ManualCommandHashes);
    }
    [Fact]
    public async Task RetryingAcceptedRemovalDoesNotDeleteAnotherFieldOrAdvanceRevisionTwice()
    {
        var (controller,session)=Fixture();var id=ImportedVoiceEditor.Document(session).Secoes[0].Itens[1].Id;
        var request=new StudioTemporaryCommandsController.ManualEdit(Guid.NewGuid().ToString(),StudioDraftService.Hash(session.ImportedDocumentJson!),"remove",[id]);
        Assert.IsType<OkObjectResult>(await controller.Apply(session.Id,request,default));
        var accepted=session.ImportedDocumentJson;
        Assert.IsType<OkObjectResult>(await controller.Apply(session.Id,request,default));
        Assert.Equal(accepted,session.ImportedDocumentJson);Assert.Equal(1,session.ImportedRevision);Assert.Single(session.ImportedUndo);
        var failure=await Assert.ThrowsAsync<StudioException>(()=>controller.Apply(session.Id,request with {Operation="undo",ItemIds=[]},default));
        Assert.Equal(409,failure.Status);Assert.Equal(accepted,session.ImportedDocumentJson);
    }
    [Fact]
    public async Task StaleHashCannotOverwriteTheCurrentSession()
    {
        var (controller,session)=Fixture();var original=session.ImportedDocumentJson;
        var failure=await Assert.ThrowsAsync<StudioException>(()=>controller.Apply(session.Id,new(Guid.NewGuid().ToString(),"old","remove",["stale"]),default));
        Assert.Equal(409,failure.Status);Assert.Equal(original,session.ImportedDocumentJson);Assert.Equal(0,session.ImportedRevision);
    }
}
