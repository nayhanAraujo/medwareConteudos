using Dapper;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public interface IPaineisWebService
{
    Task<object> ListPaineisAsync(CancellationToken ct);
}

public class PaineisWebService : IPaineisWebService
{
    private readonly IFirebirdConnectionFactory _db;
    public PaineisWebService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<object> ListPaineisAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        try
        {
            var rows = await conn.QueryAsync("SELECT CODPAINEL, NOME, TIPO FROM PAINEL ORDER BY NOME");
            return ApiResponse.Ok(rows, rows.Count());
        }
        catch
        {
            return ApiResponse.Ok(Array.Empty<object>(), 0);
        }
    }
}

[ApiController]
[Route("api/web/paineis")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class PaineisWebController : ControllerBase
{
    private readonly IPaineisWebService _svc;
    public PaineisWebController(IPaineisWebService svc) => _svc = svc;
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _svc.ListPaineisAsync(ct));
}
