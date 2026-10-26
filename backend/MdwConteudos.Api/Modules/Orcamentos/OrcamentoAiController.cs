using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ConversorHtml.Application.Configuration;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Orcamentos;

[ApiController, Authorize, Route("api/web/orcamentos/{id:long}/analises"), RequirePermission("orcamentos", "visualizar"), RequirePermission("orcamentos", "usar_ia"), TypeFilter(typeof(OrcamentoExceptionFilter))]
public sealed class OrcamentoAiController(OrcamentoService service, IOptions<OrcamentoAiOptions> options, IOptions<CursorOptions> cursor) : ControllerBase
{
    [HttpGet] public Task<object> List(long id, CancellationToken ct) { Response.Headers.CacheControl = "no-store"; return service.Analyses(id, ct); }
    [HttpPost, RequirePermission("orcamentos", "elaborar")]
    public async Task<object> Queue(long id, QuoteAiRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cursor.Value.ApiKey)) throw new OrcamentoException(503, "Credencial do Composer não configurada no servidor.");
        return new { success = true, id = await service.QueueAnalysis(id, request, options.Value, ct, classify: true) };
    }
    [HttpPost("{job:long}/cancelar"), RequirePermission("orcamentos", "elaborar")]
    public async Task<object> Cancel(long id, long job, CancellationToken ct) { await service.CancelAnalysis(id, job, ct); return new { success = true }; }
    [HttpPost("{job:long}/aplicar"), RequirePermission("orcamentos", "elaborar")]
    public async Task<object> Apply(long id, long job, QuoteAiApply request, CancellationToken ct) { await service.ApplyAnalysis(id, job, request, ct); return new { success = true, data = await service.Get(id, ct) }; }
    [HttpPost("{job:long}/confirmar"), RequirePermission("orcamentos", "elaborar")]
    public async Task<object> Confirm(long id, long job, QuoteAiConfirmation request, CancellationToken ct) => new { success = true, id = await service.ConfirmClassification(id, job, request, options.Value, ct) };
}
