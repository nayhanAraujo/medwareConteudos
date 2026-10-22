using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Auditoria;

// An old admin token must never authorize a disabled/demoted account.
public sealed class ActiveAuditAdminFilter(IFirebirdConnectionFactory db) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        { context.Result = new UnauthorizedResult(); return; }
        if (!int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        { context.Result = new ForbidResult(); return; }
        await using var conn = await db.OpenConnectionAsync(context.HttpContext.RequestAborted);
        var allowed = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM USUARIO WHERE CODUSUARIO=@id AND STATUS=-1 AND LOWER(TRIM(PERFIL))='admin'", new { id }, cancellationToken: context.HttpContext.RequestAborted));
        if (allowed != 1) context.Result = new ForbidResult();
    }
}

[ApiController, Authorize, Route("api/web/auditoria"), ServiceFilter(typeof(ActiveAuditAdminFilter))]
public sealed class AuditoriaController(AuditReader reader) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AuditQuery query, CancellationToken ct)
    {
        try
        {
            var page = await reader.ListAsync(query, ct);
            return Ok(new { success = true, data = page.Data, nextCursor = page.NextCursor });
        }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }
    [HttpGet("filtros")]
    public IActionResult Filters() => Ok(new { success = true, data = new { modules = AuditCatalog.Modules, actions = AuditCatalog.Actions, results = AuditCatalog.Results } });
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var detail = id > 0 ? await reader.GetAsync(id, ct) : null;
        return detail is null ? NotFound(new { success = false, message = "Evento não encontrado." }) : Ok(new { success = true, data = detail });
    }
}
