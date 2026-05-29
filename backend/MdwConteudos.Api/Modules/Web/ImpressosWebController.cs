using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/impressos")]
[Authorize]
public class ImpressosWebController : ControllerBase
{
    private readonly IFirebirdConnectionFactory _db;
    public ImpressosWebController(IFirebirdConnectionFactory db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        try
        {
            var rows = await conn.QueryAsync("SELECT CODIMPRESSO, NOME FROM IMPRESSO ORDER BY NOME ROWS 100");
            return Ok(ApiResponse.Ok(rows, rows.Count()));
        }
        catch
        {
            return Ok(ApiResponse.Ok(Array.Empty<object>(), 0));
        }
    }
}
