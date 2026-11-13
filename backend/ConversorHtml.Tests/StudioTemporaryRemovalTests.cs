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
    private static (StudioTemporaryCommandsController Controller,VoiceSession Session) Fixture(string currentOwner="1")
    {
        const string json="""{"camposScript":[{"id":1,"tipo":"etiqueta","nome":"A","descricao":"Paciente","coluna":1},{"id":2,"tipo":"numero","nome":"PESO","descricao":"Peso","etiquetaPai":"A","coluna":1},{"id":3,"tipo":"numero","nome":"SC","descricao":"Superfície Corporal","etiquetaPai":"A","coluna":1,"funcao":"<<VR_PESO>>*2"}]}""";
        var doc=StudioJsonImport.Parse(Encoding.UTF8.GetBytes(json),"modelo.json",[]);
        var store=new InMemoryVoiceSessionStore();
        var session=store.Create(new VoiceSession {Id=Guid.NewGuid(),OwnerUserId="1",CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow,ExpiresAt=DateTime.UtcNow.AddHours(1),CamposScriptJson=json,ImportedDocumentJson=JsonSerializer.Serialize(doc,StudioDocumentEditor.Json)});
        var manager=new RealtimeVoiceManager(Options.Create(new RealtimeVoiceOptions()),store,null!,null!,null!,NullLoggerFactory.Instance);
        var controller=new StudioTemporaryCommandsController(store,DispatchProxy.Create<IFormulasModelosService,CatalogProxy>(),manager,new StudioChoices(new EphemeralDataProtectionProvider()))
        {ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,currentOwner)],"test"))}}};
        return(controller,session);
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
