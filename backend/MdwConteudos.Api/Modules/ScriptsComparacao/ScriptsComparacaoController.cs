using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.ScriptsComparacao;

[ApiController]
[Authorize]
[Route("api/web/scripts/{scriptId:int}/comparacao-versoes")]
public sealed class ScriptsComparacaoController(IFirebirdConnectionFactory db) : ControllerBase
{
    private readonly ScriptsComparacaoService _service = new(db);

    [HttpGet]
    public async Task<IActionResult> Listar(int scriptId, CancellationToken ct)
    {
        var catalogo = await _service.ListarAsync(scriptId, ct);
        return catalogo is null
            ? NotFound(ApiResponse.Fail("Não encontrado", "Script não encontrado.", 404))
            : Ok(ApiResponse.Ok(catalogo));
    }

    [HttpGet("comparar")]
    public async Task<IActionResult> Comparar(
        int scriptId,
        [FromQuery] int versao1,
        [FromQuery] int versao2,
        CancellationToken ct)
    {
        if (versao1 <= 0 || versao2 <= 0)
            return BadRequest(ApiResponse.Fail("Validação", "Selecione duas versões para comparar."));

        try
        {
            var resultado = await _service.CompararAsync(scriptId, versao1, versao2, ct);
            return resultado is null
                ? NotFound(ApiResponse.Fail("Não encontrado", "Script não encontrado.", 404))
                : Ok(ApiResponse.Ok(resultado));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail("Não encontrado", ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail("Validação", ex.Message));
        }
    }
}
