using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Modules.RealtimeVoice;

namespace MdwConteudos.Api.Modules.StudioDrafts;

[ApiController,Authorize,StudioErrors,Route("api/voice/sessions/{id:guid}/manual")]
[RequirePermission("studio","visualizar"),RequirePermission("studio","voz")]
[EnableRateLimiting("studio")]
public sealed class StudioTemporaryCommandsController(IVoiceSessionStore store,IFormulasModelosService models,RealtimeVoiceManager realtime,StudioChoices choices) : ControllerBase
{
    public sealed record ManualEdit(string CommandId,string ExpectedHash,string Operation,string[] ItemIds,
        string? SectionId = null, string? BeforeItemId = null);
    [HttpPost,RequestSizeLimit(65536)]
    public async Task<IActionResult> Apply(Guid id,ManualEdit request,CancellationToken ct)
    {
        var owner=User.FindFirstValue(ClaimTypes.NameIdentifier);
        var session=store.Get(id);
        if(session is null||session.OwnerUserId!=owner)return NotFound(new {message="Trabalho não encontrado ou expirado."});
        if(!Guid.TryParse(request.CommandId,out _)||request.Operation is not ("remove" or "undo" or "redo" or "move")||request.ItemIds is null||request.ItemIds.Length>500||request.ItemIds.Any(i=>string.IsNullOrEmpty(i)||i.Length>120)||request.Operation=="remove"&&request.ItemIds.Length==0)
            throw new StudioException(400,"Operação manual inválida.");
        if(request.Operation == "move" && (request.ItemIds.Length != 1 || string.IsNullOrWhiteSpace(request.SectionId)
            || request.SectionId.Length > 120 || request.BeforeItemId?.Length > 120))
            throw new StudioException(400,"Informe uma medida e uma seção de destino válidas.");
        if(realtime.IsActive(id))throw new StudioException(409,"Pause e encerre a conexão de voz antes da edição manual.");
        var catalog=await models.ListVariaveisAsync(ct);
        var sha=StudioDraftService.Hash(request);
        lock(session.EditGate)
        {
            if(session.ManualCommandHashes.TryGetValue(request.CommandId,out var previous))
            {if(previous!=sha)throw new StudioException(409,"Comando já utilizado com outro conteúdo.");}
            else
            {
                if(session.ManualCommandHashes.Count>=4096)throw new StudioException(429,"Limite de operações desta sessão atingido. Baixe o modelo e abra um novo trabalho.");
                if(request.ExpectedHash!=StudioDraftService.Hash(session.ImportedDocumentJson??session.CamposScriptJson))throw new StudioException(409,"O modelo mudou. Confira a versão atual antes de editar.");
                if(request.Operation == "move")
                {
                    Move(session, request, catalog);
                }
                else if(request.Operation is "undo" or "redo")
                {
                    if(session.ImportedDocumentJson is null)throw new StudioException(400,"Este trabalho temporário não possui histórico estruturado.");
                    ImportedVoiceEditor.Apply(session,new([new(request.Operation=="undo"?"desfazer_alteracao":"refazer_alteracao",JsonSerializer.SerializeToElement(new {}))],"Alteração revertida.",[]),session.ImportedRevision,catalog);
                }
                else if(session.ImportedDocumentJson is not null)
                {
                    var before=ImportedVoiceEditor.Document(session);
                    if(!request.ItemIds.All(i=>before.Secoes.Any(s=>s.Itens.Any(n=>n.Id==i))))throw new StudioException(400,"Medida não encontrada.");
                    var after=StudioDocumentEditor.Copy(before);
                    foreach(var section in after.Secoes)section.Itens.RemoveAll(i=>request.ItemIds.Contains(i.Id));
                    StudioDependencyGuard.Ensure(before,after,catalog,"deleteItem");
                    after=StudioJsonImport.Synchronize(before,after,catalog);StudioDocumentEditor.Shape(after);
                    session.ImportedUndo.Add(session.ImportedDocumentJson);session.ImportedRedo.Clear();session.ImportedRevision++;
                    session.ImportedDocumentJson=JsonSerializer.Serialize(after,StudioDocumentEditor.Json);session.CamposScriptJson=StudioJsonImport.Export(after);
                }
                else
                {
                    var root=JsonNode.Parse(session.CamposScriptJson)!.AsObject();var fields=root["camposScript"]!.AsArray();
                    if(!request.ItemIds.All(id=>fields.Any(f=>f?["id"]?.ToString()==id&&f?["tipo"]?.ToString()=="numero")))throw new StudioException(400,"Medida não encontrada.");
                    foreach(var field in fields.Where(f=>request.ItemIds.Contains(f?["id"]?.ToString()??"")).ToArray())fields.Remove(field);
                    var after=root.ToJsonString();StudioDependencyGuard.EnsureVoice(session.CamposScriptJson,after,catalog);session.CamposScriptJson=after;session.ImportedRevision++;
                }
                session.ManualCommandHashes[request.CommandId]=sha;store.Update(session);
            }
            var dependencies=session.ImportedDocumentJson is null?StudioDependencyGuard.VoiceGraph(session.CamposScriptJson,catalog):StudioDependencyGuard.Graph(ImportedVoiceEditor.Document(session),catalog);
            return Ok(new VoiceSessionResponseDto {Id=session.Id,Mode=session.Mode,IsImported=session.ImportedDocumentJson is not null,Revision=session.ImportedRevision,
                DocumentHash=StudioDraftService.Hash(session.ImportedDocumentJson??session.CamposScriptJson),Dependencies=dependencies,CamposScriptJson=session.CamposScriptJson,
                SourceFileName=session.SourceFileName,CreatedAt=session.CreatedAt,UpdatedAt=session.UpdatedAt});
        }
    }

    private static void Move(ConversorHtml.Domain.Models.VoiceSession session, ManualEdit request, IReadOnlyList<VariavelOpcao> catalog)
    {
        var imported = session.ImportedDocumentJson is not null;
        var document = imported ? ImportedVoiceEditor.Document(session)
            : StudioJsonImport.Parse(System.Text.Encoding.UTF8.GetBytes(session.CamposScriptJson), "modelo.json", catalog);
        bool Matches(StudioImportedElement? element, string id) => element?.Properties["id"]?.ToString() == id;
        var item = document.Secoes.SelectMany(section => section.Itens)
            .SingleOrDefault(item => item.Id == request.ItemIds[0] || Matches(item.Imported, request.ItemIds[0]))
            ?? throw new StudioException(400, "Medida não encontrada.");
        var target = document.Secoes.SingleOrDefault(section => section.Id == request.SectionId || Matches(section.Imported, request.SectionId!))
            ?? throw new StudioException(400, "Seção de destino não encontrada.");
        var remaining = target.Itens.Where(candidate => candidate.Id != item.Id).ToList();
        var position = request.BeforeItemId is null ? remaining.Count
            : remaining.FindIndex(candidate => candidate.Id == request.BeforeItemId || Matches(candidate.Imported, request.BeforeItemId));
        if(position < 0) throw new StudioException(409, "A posição de destino mudou. Confira o modelo e tente novamente.");
        if(imported)
        {
            ImportedVoiceEditor.Apply(session, new([new("mover_medida", JsonSerializer.SerializeToElement(new {
                itemId = item.Id, secaoId = target.Id, posicao = position }))], "Medida movida.", []), session.ImportedRevision, catalog);
        }
        else
        {
            var after = StudioDocumentEditor.Apply(document, new(request.CommandId, session.ImportedRevision, "moveItem",
                JsonSerializer.SerializeToElement(new { itemId = item.Id, sectionId = target.Id, position })), catalog);
            var json = StudioJsonImport.Export(after);
            StudioDependencyGuard.EnsureVoice(session.CamposScriptJson, json, catalog);
            session.CamposScriptJson = json;
            session.ImportedRevision++;
        }
    }

    [HttpPost("escolhas"),RequestSizeLimit(16384)]
    public async Task<IActionResult> Choose(Guid id,StudioChoice request,CancellationToken ct)
    {
        var owner=User.FindFirstValue(ClaimTypes.NameIdentifier);var session=store.Get(id);
        if(session is null||session.OwnerUserId!=owner)return NotFound();
        if(!Guid.TryParse(request.CommandId,out _)||string.IsNullOrWhiteSpace(request.SectionId)||request.Token.Length>8192)throw new StudioException(400,"Escolha inválida.");
        var sha=StudioDraftService.Hash(request);var catalog=await models.ListVariaveisAsync(ct);
        lock(session.EditGate)
        {
            if(session.ManualCommandHashes.TryGetValue(request.CommandId,out var prior))
            {if(prior!=sha)throw new StudioException(409,"Comando reutilizado com outro conteúdo.");}
            else
            {
                if(session.ManualCommandHashes.Count>=4096)throw new StudioException(429,"Limite de operações desta sessão atingido. Baixe o modelo e abra um novo trabalho.");
                if(realtime.IsActive(id))throw new StudioException(409,"Encerre a conexão de voz antes de confirmar a escolha.");
                var revision=choices.Validate(request.Token,id.ToString(),int.Parse(owner!),request.CodVariavel);
                if(revision!=session.ImportedRevision)throw new StudioException(409,"O modelo mudou. Cancele estas sugestões e solicite novamente.");
                if(session.ImportedDocumentJson is not null)
                    ImportedVoiceEditor.Apply(session,new([new("incluir_medidas",JsonSerializer.SerializeToElement(new {secaoId=request.SectionId,ids=new[]{request.CodVariavel}}))],"Medida incluída.",[]),revision,catalog);
                else
                {
                    if(!int.TryParse(request.SectionId,out var section))throw new StudioException(400,"Seção inválida.");
                    var draft=new RealtimeDraft(session.CamposScriptJson,revision);
                    draft.Apply("incluir_medidas",JsonSerializer.SerializeToElement(new {grupoOperacao=request.CommandId,substituirOperacao=false,secaoId=section,codVariaveis=new[]{request.CodVariavel}}),catalog);
                    session.CamposScriptJson=draft.Render();session.ImportedRevision=draft.Revision;
                }
                session.ManualCommandHashes[request.CommandId]=sha;store.Update(session);
            }
        }
        return Ok(new {revision=session.ImportedRevision});
    }

    public sealed record CancelRequest(string Token);
    [HttpPost("escolhas/cancelar"),RequestSizeLimit(16384)]
    public IActionResult Cancel(Guid id,CancelRequest request)
    {
        var session=store.Get(id);
        if(session is null||session.OwnerUserId!=User.FindFirstValue(ClaimTypes.NameIdentifier))return NotFound();
        realtime.CompleteChoice(id,request.Token);return Ok();
    }
}
