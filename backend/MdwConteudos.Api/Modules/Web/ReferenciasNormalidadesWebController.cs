using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/variaveis/referencias-normalidades")]
[Authorize]
public class ReferenciasNormalidadesWebController : ControllerBase
{
    private readonly IReferenciasService _svc;

    public ReferenciasNormalidadesWebController(IReferenciasService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<IActionResult> GetPainel(
        [FromQuery] int? referenciaId = null,
        [FromQuery] string? referenciaBusca = null,
        [FromQuery] string? variavelBusca = null,
        [FromQuery] int limite = 50,
        CancellationToken ct = default
    )
    {
        try
        {
            var data = await _svc.GetReferenciasNormalidadesAsync(
                referenciaId,
                referenciaBusca,
                variavelBusca,
                limite,
                ct
            );
            return Ok(new { success = true, data });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("vincular")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Vincular([FromBody] VincularNormalidadesRequest req, CancellationToken ct = default)
    {
        try
        {
            if (req.CodReferencia <= 0)
                return BadRequest(new { success = false, message = "Selecione uma referência válida." });

            var total = await _svc.VincularNormalidadesAsync(
                req.CodReferencia,
                req.Normalidades ?? [],
                User.GetCodUsuario(),
                ct
            );
            return Ok(new { success = true, message = $"{total} normalidade(s) vinculada(s) com sucesso.", total });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("atualizar")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Atualizar([FromBody] AtualizarNormalidadeReferenciaRequest req, CancellationToken ct = default)
    {
        try
        {
            await _svc.AtualizarNormalidadeReferenciaAsync(req, User.GetCodUsuario(), ct);
            return Ok(new { success = true, message = "Normalidade atualizada com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("desvincular")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Desvincular([FromBody] DesvincularNormalidadeReferenciaRequest req, CancellationToken ct = default)
    {
        try
        {
            await _svc.DesvincularNormalidadeAsync(req.CodNormalidade, User.GetCodUsuario(), ct);
            return Ok(new { success = true, message = "Normalidade desvinculada da referência." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("importar")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Importar([FromBody] ImportarNormalidadesRequest req, CancellationToken ct = default)
    {
        try
        {
            var data = await _svc.ImportarNormalidadesAsync(
                req.CodReferenciaDestino,
                req.CodReferenciaOrigem,
                User.GetCodUsuario(),
                ct
            );
            return Ok(new { success = true, data });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
