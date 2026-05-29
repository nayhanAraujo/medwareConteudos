using Dapper;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public interface IConteudosWebService
{
    Task<object> ListGruposFrasesAsync(CancellationToken ct);
}

public class ConteudosWebService : IConteudosWebService
{
    private readonly IFirebirdConnectionFactory _db;
    public ConteudosWebService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<object> ListGruposFrasesAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        try
        {
            var rows = await conn.QueryAsync("SELECT CODGRUPO, NOME FROM GRUPO_FRASES ORDER BY NOME");
            return ApiResponse.Ok(rows, rows.Count());
        }
        catch
        {
            return ApiResponse.Ok(Array.Empty<object>(), 0);
        }
    }
}

[ApiController]
[Route("api/web/conteudos")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class ConteudosWebController : ControllerBase
{
    private readonly IConteudosWebService _svc;
    public ConteudosWebController(IConteudosWebService svc) => _svc = svc;

    [HttpGet("grupos-frases")]
    public async Task<IActionResult> GruposFrases(CancellationToken ct) => Ok(await _svc.ListGruposFrasesAsync(ct));
}
