using Dapper;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public interface IRelatoriosWebService
{
    Task<object> ListAsync(CancellationToken ct);
}

public class RelatoriosWebService : IRelatoriosWebService
{
    private readonly IFirebirdConnectionFactory _db;
    public RelatoriosWebService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<object> ListAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync(
            "SELECT CODRELATORIO, NOME, MODULO, FORMATO, ATIVO FROM RELATORIOS ORDER BY DTHRCRIACAO DESC ROWS 100");
        return ApiResponse.Ok(rows, rows.Count());
    }
}

[ApiController]
[Route("api/web/relatorios")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class RelatoriosWebController : ControllerBase
{
    private readonly IRelatoriosWebService _svc;
    public RelatoriosWebController(IRelatoriosWebService svc) => _svc = svc;
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _svc.ListAsync(ct));
}
