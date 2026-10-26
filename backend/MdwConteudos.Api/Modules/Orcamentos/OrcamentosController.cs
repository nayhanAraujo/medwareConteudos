using System.Security.Claims;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed class OrcamentoExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is OrcamentoException ex)
        {
            context.Result = new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = ex.Status }; context.ExceptionHandled = true;
        }
        else if (context.Exception is FbException fb && fb.ErrorCode is 335544336 or 335544345 or 335544451 or 335544878 or 335544665 or 335544349)
        {
            context.Result = new ConflictObjectResult(new { success = false, message = "O registro foi alterado ou possui um vínculo concorrente. Atualize antes de continuar." }); context.ExceptionHandled = true;
        }
    }
}

[ApiController, Authorize, Route("api/web/orcamentos"), RequirePermission("orcamentos", "visualizar"), TypeFilter(typeof(OrcamentoExceptionFilter))]
public sealed class OrcamentosController(OrcamentoService service, IPermissionService permissions) : ControllerBase
{
    private async Task Check(string action, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || !await permissions.HasPermissionAsync(id, User.FindFirstValue(ClaimTypes.Role) ?? "", "orcamentos", action, ct))
            throw new OrcamentoException(403, "Usuário sem permissão para esta ação de orçamentos.");
    }
    [HttpGet] public Task<object> List([FromQuery] string? status, [FromQuery] long? before, [FromQuery] bool archived, CancellationToken ct) => service.List(status, before, archived, ct);
    [HttpGet("{id:long}")] public async Task<object> Get(long id, CancellationToken ct) => new { success = true, data = await service.Get(id, ct) };
    [HttpPost, RequirePermission("orcamentos", "elaborar")]
    public async Task<object> Create(CreateQuoteRequest request, CancellationToken ct) => new { success = true, id = (await service.Create(request, ct)).ToString(System.Globalization.CultureInfo.InvariantCulture) };
    [HttpPut("{id:long}"), RequirePermission("orcamentos", "elaborar")]
    public async Task<object> Save(long id, SaveQuoteRequest request, CancellationToken ct) { await service.Save(id, request, ct); return new { success = true, data = await service.Get(id, ct) }; }
    [HttpPost("{id:long}/transicoes")]
    public async Task<object> Command(long id, QuoteCommand request, CancellationToken ct)
    {
        await Check(OrcamentoWorkflow.Permission(request.Action), ct); await service.Command(id, request, ct); return new { success = true, data = await service.Get(id, ct) };
    }
    [HttpGet("clientes")] public async Task<object> Clients(CancellationToken ct) => new { success = true, data = await service.Clients(ct) };
    [HttpGet("clientes/{id:int}/comercial"), RequirePermission("orcamentos", "configurar_clientes")]
    public async Task<object> Client(int id, CancellationToken ct) => new { success = true, data = await service.GetClient(id, ct) };
    [HttpPut("clientes/{id:int}/comercial"), RequirePermission("orcamentos", "configurar_clientes")]
    public async Task<object> ClientSave(int id, SaveClientRequest request, CancellationToken ct) { await service.SaveClient(id, request, ct); return new { success = true, data = await service.GetClient(id, ct) }; }
    [HttpGet("configuracoes")]
    public async Task<object> Policy(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !(await permissions.HasPermissionAsync(id, User.FindFirstValue(ClaimTypes.Role) ?? "", "orcamentos", "elaborar", ct) ||
              await permissions.HasPermissionAsync(id, User.FindFirstValue(ClaimTypes.Role) ?? "", "orcamentos", "configurar_precos", ct))) throw new OrcamentoException(403, "Sem permissão para consultar a política comercial.");
        return new { success = true, data = await service.GetPolicy(ct) };
    }
    [HttpPut("configuracoes"), RequirePermission("orcamentos", "configurar_precos")]
    public async Task<object> PolicySave(SavePolicyRequest request, CancellationToken ct) { await service.SavePolicy(request, ct); return new { success = true, data = await service.GetPolicy(ct) }; }
    [HttpPost("{id:long}/anexos"), RequestSizeLimit(22 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 22 * 1024 * 1024)]
    public async Task<object> Upload(long id, [FromForm] string version, [FromForm] string kind, IFormFile file, CancellationToken ct)
    {
        await Check(kind == "escopo" ? "elaborar" : "registrar_marcos", ct); await service.Upload(id, version, kind, file, ct); return new { success = true, data = await service.Get(id, ct) };
    }
    [HttpGet("{id:long}/anexos/{file:long}")]
    public async Task<IActionResult> Download(long id, long file, CancellationToken ct)
    {
        var result = await service.Download(id, file, ct); PrivateDownload(); return File(result.Bytes, result.Mime, result.Name);
    }
    [HttpGet("{id:long}/proposta")]
    public async Task<object> Proposal(long id, [FromQuery] int? revision, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store"; var proposal = await service.Proposal(id, revision, ct); return new { success = true, data = proposal.Document, email = proposal.Email, superseded = proposal.Superseded };
    }
    [HttpPost("{id:long}/proposta"), RequirePermission("orcamentos", "registrar_marcos")]
    public async Task<object> Prepare(long id, PrepareQuoteRequest request, CancellationToken ct)
    {
        await service.PrepareProposal(id, request.Version, ct); return new { success = true, data = await service.Get(id, ct) };
    }
    // Email is an explicit write, never triggered by preview, download or approval.
    [HttpPost("{id:long}/proposta/enviar-email"), RequirePermission("orcamentos", "registrar_marcos")]
    public async Task<object> SendEmail(long id, SendQuoteEmailRequest request, [FromServices] IQuoteEmailSender sender, CancellationToken ct)
    {
        await Check("registrar_marcos", ct);
        await service.SendProposalEmail(id, request, sender, ct);
        return new { success = true, data = await service.Get(id, ct) };
    }
    [HttpGet("{id:long}/proposta.pdf")]
    public async Task<IActionResult> Pdf(long id, [FromQuery] int? revision, CancellationToken ct)
    {
        var proposal = await service.Proposal(id, revision, ct); PrivateDownload(); return File(proposal.Pdf, "application/pdf", $"orcamento-{proposal.Document.Id}-r{proposal.Document.Revision}.pdf");
    }
    private void PrivateDownload() { Response.Headers.CacheControl = "no-store"; Response.Headers.XContentTypeOptions = "nosniff"; }
}
public sealed record PrepareQuoteRequest(string Version);
