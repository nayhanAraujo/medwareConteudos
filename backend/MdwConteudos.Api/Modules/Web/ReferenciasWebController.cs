using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/referencias")]
[Authorize]
public class ReferenciasWebController : ControllerBase
{
    private readonly IFirebirdConnectionFactory _db;
    public ReferenciasWebController(IFirebirdConnectionFactory db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync(
            "SELECT CODREFERENCIA, TITULO, ANO FROM REFERENCIA ORDER BY ANO DESC ROWS 100");
        return Ok(ApiResponse.Ok(rows, rows.Count()));
    }
}
