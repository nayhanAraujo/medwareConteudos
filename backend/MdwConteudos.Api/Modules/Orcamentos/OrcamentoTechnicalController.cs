using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Orcamentos;

[ApiController, Authorize, Route("api/web/orcamentos/regras-tecnicas"), RequirePermission("orcamentos", "visualizar"), TypeFilter(typeof(OrcamentoExceptionFilter))]
public sealed class OrcamentoTechnicalController(OrcamentoService service) : ControllerBase
{
    [HttpGet, RequirePermission("orcamentos", "configurar_regras")] public Task<object> Get(CancellationToken ct) { Response.Headers.CacheControl = "no-store"; return service.GetTechnicalCatalog(ct); }
    [HttpGet("aplicaveis"), RequirePermission("orcamentos", "usar_ia")]
    public Task<object> Applicable(CancellationToken ct) { Response.Headers.CacheControl = "no-store"; return service.GetTechnicalCatalog(ct, applicableOnly: true); }
    [HttpPut, RequirePermission("orcamentos", "configurar_regras")]
    public async Task<object> Publish(QuoteCatalogPublication request, CancellationToken ct) { await service.PublishTechnicalCatalog(request, ct); return await service.GetTechnicalCatalog(ct); }
}
