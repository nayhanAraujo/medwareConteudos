using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public sealed record JsonImportConfirmation(string Destination, string CommandId, string Nome, Dictionary<string,int>? Associations = null);
public sealed class StudioJsonReviews(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public sealed class Review(int owner, StudioDocument document, DateTime createdAt)
    {
        public int Owner { get; } = owner;
        public StudioDocument Document { get; } = document;
        public DateTime Expires { get; } = createdAt.AddMinutes(15);
        public SemaphoreSlim Gate { get; } = new(1);
        public string? ConfirmationHash { get; set; }
        public object? Result { get; set; }
        public string WorkId { get; } = Guid.NewGuid().ToString();
    }
    private readonly ConcurrentDictionary<Guid,Review> _reviews = new();
    private readonly object _gate = new();
    public Guid Add(int owner, StudioDocument document)
    {
        lock (_gate)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            foreach (var old in _reviews.Where(p => p.Value.Expires <= now)) _reviews.TryRemove(old.Key, out _);
            if (_reviews.Count >= 128 || _reviews.Values.Count(r => r.Owner == owner) >= 8) throw new StudioException(429, "Há muitas importações em revisão. Aguarde a expiração antes de enviar outro arquivo.");
            var id = Guid.NewGuid(); _reviews[id] = new(owner, document, now); return id;
        }
    }
    public Review Get(Guid id, int owner) => _reviews.TryGetValue(id, out var r) && r.Owner == owner && r.Expires > _clock.GetUtcNow().UtcDateTime ? r : throw new StudioException(404, "Revisão não encontrada ou expirada. Importe o arquivo novamente.");
}

[ApiController, Route("api/voice/importacoes/json"), Authorize, StudioErrors]
[RequirePermission("studio", "visualizar"), RequirePermission("studio", "voz")]
[EnableRateLimiting("studio")]
public sealed class StudioJsonImportController(StudioJsonReviews reviews, IFormulasModelosService models, StudioDraftService drafts, IVoiceSessionStore voice, IConfiguration config) : ControllerBase
{
    private int Owner => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPost, RequestSizeLimit(StudioJsonImport.MaxBytes + 65536)]
    public async Task<IActionResult> Review(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > StudioJsonImport.MaxBytes) throw new StudioException(400, "Use um JSON de até 1 MiB.");
        await using var stream = file.OpenReadStream(); using var buffer = new MemoryStream(); await stream.CopyToAsync(buffer, ct);
        var catalog = await models.ListVariaveisAsync(ct);
        var doc = StudioJsonImport.Parse(buffer.ToArray(), file.FileName, catalog);
        var id = reviews.Add(Owner, doc); var review = reviews.Get(id, Owner);
        return Ok(new { id, expiresAt = review.Expires, document = doc, elementCount = StudioJsonImport.Elements(doc).Count(),
            warnings = StudioJsonImport.Problems(doc), associations = doc.Secoes.SelectMany(s => s.Itens).Select(i => new {
                itemId = i.Id, selected = i.CodVariavel, candidates = StudioJsonImport.Candidates(i.Imported!.Properties, catalog).Select(v => new { v.CodVariavel, v.Nome, v.Unidade, v.Grupo }) }) });
    }
    [HttpPost("{id:guid}/confirmar")]
    public async Task<IActionResult> Confirm(Guid id, JsonImportConfirmation request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.CommandId, out _) || request.Destination is not ("conventional" or "integrated")) throw new StudioException(400, "Destino ou comando inválido.");
        var review = reviews.Get(id, Owner); await review.Gate.WaitAsync(ct);
        try
        {
            reviews.Get(id, Owner); // The review may have expired while waiting for another confirmation.
            var hash = StudioDraftService.Hash(new { request.CommandId, request.Destination, request.Nome, associations = request.Associations?.OrderBy(p => p.Key).ToArray() });
            if (review.ConfirmationHash is not null && review.ConfirmationHash != hash) throw new StudioException(409, "Esta revisão já foi usada com outra confirmação.");
            if (review.Result is not null) return Ok(review.Result);
            var doc = StudioDocumentEditor.Copy(review.Document) with { Nome = StudioDocumentEditor.Title(request.Nome) };
            var catalog = await models.ListVariaveisAsync(ct);
            foreach (var choice in request.Associations ?? [])
            {
                var section = doc.Secoes.FirstOrDefault(s => s.Itens.Any(i => i.Id == choice.Key)) ?? throw new StudioException(400, "Escolha de associação inválida.");
                var item = section.Itens.First(i => i.Id == choice.Key);
                if (!StudioJsonImport.Candidates(item.Imported!.Properties, catalog).Any(v => v.CodVariavel == choice.Value)) throw new StudioException(409, "As opções do catálogo mudaram. Revise o arquivo novamente.");
                section.Itens[section.Itens.IndexOf(item)] = item with { CodVariavel = choice.Value };
            }
            review.ConfirmationHash = hash;
            object result;
            if (request.Destination == "integrated")
            {
                if (!await drafts.Available(ct)) throw new StudioException(503, "Rascunhos integrados indisponíveis.");
                result = new { destination = request.Destination, draft = await drafts.Create(Owner, new(doc.Nome), ct, doc, review.WorkId) };
            }
            else
            {
                var now = DateTime.UtcNow;
                var session = voice.Get(Guid.Parse(review.WorkId)) ?? voice.Create(new VoiceSession { Id = Guid.Parse(review.WorkId), OwnerUserId = Owner.ToString(), Mode = VoiceSessionMode.FromJson,
                    ImportedDocumentJson = JsonSerializer.Serialize(doc, StudioDocumentEditor.Json), CamposScriptJson = StudioJsonImport.Export(doc),
                    SourceFileName = doc.ImportedJson!.FileName, CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddMinutes(Math.Clamp(config.GetValue("Voice:SessionTtlMinutes", 120), 1, 1440)) });
                result = new { destination = request.Destination, session = new VoiceSessionResponseDto { Id = session.Id, Mode = session.Mode, CamposScriptJson = session.CamposScriptJson,
                    IsImported = true, Revision = session.ImportedRevision, DocumentHash = StudioDraftService.Hash(session.ImportedDocumentJson!), Dependencies = StudioDependencyGuard.Graph(doc,catalog), SourceFileName = session.SourceFileName, CreatedAt = session.CreatedAt, UpdatedAt = session.UpdatedAt } };
            }
            review.Result = result; return Ok(result);
        }
        finally { review.Gate.Release(); }
    }
}
