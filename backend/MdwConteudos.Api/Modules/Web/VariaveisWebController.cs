using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/variaveis")]
[Authorize]
public class VariaveisWebController : ControllerBase
{
    private readonly IVariaveisWebService _svc;
    public VariaveisWebController(IVariaveisWebService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        [FromQuery] string? search = null,
        [FromQuery] int? grupo = null,
        CancellationToken ct = default)
        => Ok(await _svc.ListVariaveisAsync(skip, take, search, grupo, ct));

    [HttpGet("grupos")]
    public async Task<IActionResult> ListGrupos(CancellationToken ct)
        => Ok(await _svc.ListGruposAsync(ct));

    [HttpGet("meta")]
    public async Task<IActionResult> Meta(CancellationToken ct)
        => Ok(await _svc.GetMetaAsync(ct));

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] VariavelCreateRequest req, CancellationToken ct)
    {
        try
        {
            var cod = await _svc.CreateVariavelAsync(req, User.GetCodUsuario(), ct);
            return Ok(new { success = true, codVariavel = cod });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/edicao")]
    public async Task<IActionResult> GetEdicao(int id, CancellationToken ct)
    {
        var data = await _svc.GetVariavelEdicaoAsync(id, ct);
        return data is null ? NotFound(new { message = "Variável não encontrada." }) : Ok(data);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] VariavelCreateRequest req, CancellationToken ct)
    {
        try
        {
            await _svc.UpdateVariavelAsync(id, req, User.GetCodUsuario(), ct);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/detalhes-completos")]
    public async Task<IActionResult> GetDetalhesCompletos(int id, CancellationToken ct)
        => Ok(await _svc.GetDetalhesCompletosAsync(id, ct));

    [HttpGet("{id:int}/codigos-vinculados")]
    public async Task<IActionResult> GetCodigosVinculados(int id, CancellationToken ct)
        => Ok(await _svc.GetCodigosVinculadosAsync(id, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var row = await _svc.GetVariavelAsync(id, ct);
        return row is null ? NotFound() : Ok(ApiResponse.Ok(row));
    }
}
