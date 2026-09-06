using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Assistente.Vinculos;

[ApiController, Authorize]
[Route("api/web/assistente")]
public sealed class AssistenteVinculosController(IAssistenteVinculosService service) : ControllerBase
{
    [HttpGet("{domain}/{id:int}/vinculos")]
    public async Task<IActionResult> Get(string domain, int id, CancellationToken ct)
    {
        try { var result = await service.Get(domain, id, ct); return result is null ? NotFound(ApiResponse.Fail("Não encontrado")) : Ok(ApiResponse.Ok(result)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail("Requisição inválida", ex.Message)); }
    }

    [HttpGet("vinculos/resumo")]
    public async Task<IActionResult> Summary([FromQuery] string domain, [FromQuery] string? search = null,
        [FromQuery] string filter = "todos", [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try { return Ok(ApiResponse.Ok(await service.Summary(domain, search, filter, page, pageSize, ct))); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail("Requisição inválida", ex.Message)); }
    }

    [HttpPut("{domain}/{id:int}/vinculos/{relation}"), RequirePermission(PermissionDomains.Assistente, PermissionActions.Vincular)]
    public async Task<IActionResult> Set(string domain, int id, string relation, [FromBody] SetVinculosRequest request, CancellationToken ct)
    {
        try { await service.Set(domain, id, relation, request.Ids ?? [], request.Items, ct); return Ok(ApiResponse.OkMessage("Vínculos atualizados.")); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail("Requisição inválida", ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail("Validação", ex.Message)); }
    }
}

public sealed record SetVinculosRequest(IReadOnlyList<int>? Ids, IReadOnlyList<SequencedItem>? Items);
