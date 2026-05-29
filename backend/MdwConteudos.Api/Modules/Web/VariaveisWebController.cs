using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/variaveis")]
[Authorize]
public class VariaveisWebController : ControllerBase
{
    private readonly IVariaveisWebService _svc;
    public VariaveisWebController(IVariaveisWebService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await _svc.ListVariaveisAsync(skip, take, search, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var row = await _svc.GetVariavelAsync(id, ct);
        return row is null ? NotFound() : Ok(ApiResponse.Ok(row));
    }
}
