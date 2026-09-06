using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.FormulasModelos;

[ApiController]
[Route("api/web/formulas")]
[Authorize]
public sealed class FormulasController : ControllerBase
{
    private readonly IFormulasModelosService _service;

    public FormulasController(IFormulasModelosService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sigla = null,
        [FromQuery] string? nome = null,
        [FromQuery] string? formula = null,
        CancellationToken ct = default)
    {
        var result = await _service.ListFormulasAsync(page, pageSize, sigla, nome, formula, ct);
        return Ok(new
        {
            success = true,
            data = result.Items,
            result.Page,
            result.PageSize,
            result.TotalItems,
            result.TotalPages
        });
    }

    [HttpGet("meta")]
    public async Task<IActionResult> Meta(CancellationToken ct)
        => Ok(ApiResponse.Ok(await _service.GetFormulaMetaAsync(ct)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await _service.GetFormulaAsync(id, ct);
        return result is null
            ? NotFound(ApiResponse.Fail("not_found", "Fórmula não encontrada.", 404))
            : Ok(ApiResponse.Ok(result));
    }

    [HttpPost]
    [RequirePermission(PermissionDomains.Formulas, PermissionActions.Criar)]
    public async Task<IActionResult> Create([FromBody] FormulaUpsertRequest request, CancellationToken ct)
    {
        try
        {
            var id = await _service.CreateFormulaAsync(request, User.GetCodUsuario(), ct);
            return Ok(ApiResponse.Ok(new { codFormula = id }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail("validation_error", ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionDomains.Formulas, PermissionActions.Editar)]
    public async Task<IActionResult> Update(int id, [FromBody] FormulaUpsertRequest request, CancellationToken ct)
    {
        try
        {
            await _service.UpdateFormulaAsync(id, request, User.GetCodUsuario(), ct);
            return Ok(ApiResponse.OkMessage("Fórmula atualizada com sucesso."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail("validation_error", ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionDomains.Formulas, PermissionActions.Excluir)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteFormulaAsync(id, ct);
            return Ok(ApiResponse.OkMessage("Fórmula excluída com sucesso."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail("validation_error", ex.Message));
        }
    }
}
