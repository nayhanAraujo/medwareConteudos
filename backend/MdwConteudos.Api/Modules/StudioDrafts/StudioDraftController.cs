using System.Security.Claims;
using System.Text;
using ConversorHtml.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Modules.FormulasModelos;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public sealed class StudioErrors : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is StudioException e)
            context.Result = new ObjectResult(new { message = e.Message }) { StatusCode = e.Status };
        else if (context.Exception is ArgumentException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
            context.Result = new BadRequestObjectResult(new { message = context.Exception.Message });
        else if (context.Exception is FirebirdSql.Data.FirebirdClient.FbException f &&
                 (f.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) || f.Message.Contains("update conflict", StringComparison.OrdinalIgnoreCase)))
            context.Result = new ConflictObjectResult(new { message = "Edição concorrente. Recarregue o rascunho e confira o resultado do comando." });
        else return;
        context.ExceptionHandled = true;
    }
}

[ApiController, Route("api/voice/rascunhos"), Authorize, StudioErrors, RequestSizeLimit(2 * 1024 * 1024)]
[RequirePermission("studio", "visualizar")]
public sealed class StudioDraftController(StudioDraftService drafts, IFormulasModelosService models, IImageMeasureAnalyzer analyzer, StudioChoices choices) : ControllerBase
{
    private int Owner => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("status")] public async Task<IActionResult> Status(CancellationToken ct) => Ok(new { enabled = await drafts.Available(ct) });
    [HttpGet("catalogo")] public async Task<IActionResult> Search([FromQuery] string term, CancellationToken ct) => Ok(await drafts.Search(term, ct));
    [HttpGet("publicacoes")] public async Task<IActionResult> Publications(CancellationToken ct) => Ok(await drafts.Publications(Owner, ct));
    [HttpDelete("publicacoes/{publicationId:guid}")]
    public async Task<IActionResult> DeletePublication(Guid publicationId, [FromQuery] bool confirm, CancellationToken ct)
    {
        if (!confirm) return BadRequest(new { message = "Confirme a exclusão desta versão publicada. O modelo atual não será excluído." });
        await drafts.DeletePublication(publicationId.ToString(), Owner, ct); return NoContent();
    }
    [HttpGet] public async Task<IActionResult> List([FromQuery] bool archived, CancellationToken ct) => Ok(await drafts.List(Owner, archived, ct));
    [HttpPost] public async Task<IActionResult> Create(DraftCreate request, CancellationToken ct) => Ok(await drafts.Create(Owner, request, ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await drafts.Get(id.ToString(), Owner, ct));
    [HttpPost("{id:guid}/comandos")] public async Task<IActionResult> Command(Guid id, DraftCommand request, CancellationToken ct) => Ok(await drafts.Command(id.ToString(), Owner, request, ct));
    [HttpPost("{id:guid}/escolhas")]
    public async Task<IActionResult> ResolveChoice(Guid id, StudioChoice request, CancellationToken ct)
    {
        var revision = choices.Validate(request.Token, id.ToString(), Owner, request.CodVariavel);
        return Ok(await drafts.Command(id.ToString(), Owner, new(request.CommandId, revision, "addMeasures",
            System.Text.Json.JsonSerializer.SerializeToElement(new { sectionId = request.SectionId, ids = new[] { request.CodVariavel } })), ct));
    }
    [HttpGet("{id:guid}/comandos/{commandId:guid}")]
    public async Task<IActionResult> CommandResult(Guid id, Guid commandId, CancellationToken ct)
    {
        var item = (await drafts.History(id.ToString(), Owner, ct)).FirstOrDefault(r => r.CommandId.Trim() == commandId.ToString());
        return item is null ? NotFound(new { message = "Comando ainda não confirmado." }) : Ok(new { revision = item.Number, document = System.Text.Json.JsonSerializer.Deserialize<StudioDocument>(item.Document, StudioDocumentEditor.Json) });
    }
    [HttpGet("{id:guid}/historico")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) => Ok((await drafts.History(id.ToString(), Owner, ct, false)).Select(h => new { revision = h.Number, commandId = h.CommandId.Trim(), type = h.Type, source = h.Source, group = h.Group.Trim(), description = h.Description, createdAt = DateTime.SpecifyKind(h.CreatedAt, DateTimeKind.Utc) }));
    [HttpGet("{id:guid}/historico/{revision:int}")]
    public async Task<IActionResult> Revision(Guid id, int revision, CancellationToken ct)
    {
        var h = await drafts.Revision(id.ToString(), Owner, revision, ct);
        return Ok(new { revision = h.Number, document = System.Text.Json.JsonSerializer.Deserialize<StudioDocument>(h.Document, StudioDocumentEditor.Json) });
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] long revision, [FromQuery] bool confirm, CancellationToken ct)
    {
        if (!confirm) return BadRequest(new { message = "Confirme a exclusão do rascunho e de todo seu histórico de trabalho. Publicações serão preservadas." });
        await drafts.Delete(id.ToString(), Owner, revision, ct); return NoContent();
    }
    [HttpGet("{id:guid}/validacao")] public async Task<IActionResult> Validate(Guid id, CancellationToken ct) => Ok(await drafts.Validate(id.ToString(), Owner, ct));
    [HttpPost("{id:guid}/exportar")]
    public async Task<IActionResult> Export(Guid id, ExportRequest request, [FromQuery] bool download, CancellationToken ct)
    {
        var result = await drafts.Export(id.ToString(), Owner, request.Revision, request.Format, ct);
        Response.Headers["X-Studio-Revision"] = result.Revision.ToString(); Response.Headers["X-Studio-Metadata"] = result.MetadataHash;
        return download ? File(Encoding.UTF8.GetBytes(result.File.Conteudo), result.File.ContentType, result.File.NomeArquivo)
            : Ok(new { content = result.File.Conteudo, format = request.Format, revision = result.Revision, metadataHash = result.MetadataHash });
    }
    [HttpPost("{id:guid}/publicacoes")]
    public async Task<IActionResult> Prepare(Guid id, PublicationRequest request, CancellationToken ct) => Ok(await drafts.Prepare(id.ToString(), Owner, request, ct));
    [HttpPost("{id:guid}/publicacoes/confirmar")]
    public async Task<IActionResult> Confirm(Guid id, PublicationConfirmation request, CancellationToken ct)
    {
        var permission = HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
        // New copies and updates are distinct permissions; the service also verifies ownership.
        var action = await drafts.PublicationCreatesModel(id.ToString(), Owner, request.ProposalId, ct) ? PermissionActions.Criar : PermissionActions.Editar;
        if (!await permission.HasPermissionAsync(Owner, role, PermissionDomains.Formulas, action, ct)) return Forbid();
        return Ok(await drafts.Confirm(id.ToString(), Owner, request.ProposalId, ct));
    }
    [HttpPost("{id:guid}/imagem"), RequestSizeLimit(10 * 1024 * 1024)]
    [RequirePermission("studio", "converter")]
    public async Task<IActionResult> Image(Guid id, IFormFile image, CancellationToken ct)
    {
        var draft = await drafts.Get(id.ToString(), Owner, ct);
        if (image.Length is <= 0 or > 10 * 1024 * 1024 || image.ContentType is not ("image/png" or "image/jpeg" or "image/webp")) return BadRequest(new { message = "Use PNG, JPEG ou WebP de até 10 MB." });
        await using var stream = image.OpenReadStream();
        var extracted = await analyzer.AnalyzeAsync(stream, image.FileName, ct);
        var catalog = await models.ListVariaveisAsync(ct);
        var sections = extracted.Measures.GroupBy(m => m.Section ?? "Dados gerais").Select(g => new StudioSection(StudioDocumentEditor.Token(), g.Key, 1,
            g.Select(m => { var found = catalog.Where(v => string.Equals(v.Nome, m.Label, StringComparison.OrdinalIgnoreCase) || string.Equals(v.Codigo ?? v.Sigla, m.VariableName, StringComparison.OrdinalIgnoreCase)).ToArray();
                return new StudioItem(StudioDocumentEditor.Token(), found.Length == 1 ? found[0].CodVariavel : null, m.Label); }).ToList())).ToArray();
        return Ok(new { revision = draft.Revision, sections }); // Review only: applying requires a separate versioned command.
    }
}
