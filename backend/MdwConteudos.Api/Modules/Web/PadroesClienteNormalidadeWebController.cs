using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/variaveis/padroes-cliente")]
[Authorize]
public class PadroesClienteNormalidadeWebController : ControllerBase
{
    private readonly IPadroesClienteNormalidadeService _svc;

    public PadroesClienteNormalidadeWebController(IPadroesClienteNormalidadeService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetPainel(
        [FromQuery] int codCliente,
        [FromQuery] int? codPadrao = null,
        [FromQuery] string? variavelBusca = null,
        CancellationToken ct = default)
    {
        try
        {
            var data = await _svc.GetPainelAsync(codCliente, codPadrao, variavelBusca, ct);
            return Ok(new { success = true, data });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreatePadraoClienteRequest req, CancellationToken ct = default)
    {
        try
        {
            var codPadrao = await _svc.CreatePadraoAsync(req, User.GetCodUsuario(), ct);
            return Ok(new { success = true, codPadrao, message = "Padrão criado com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{codPadrao:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int codPadrao, [FromBody] UpdatePadraoClienteRequest req, CancellationToken ct = default)
    {
        try
        {
            await _svc.UpdatePadraoAsync(codPadrao, req, User.GetCodUsuario(), ct);
            return Ok(new { success = true, message = "Padrão atualizado com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("{codPadrao:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int codPadrao, CancellationToken ct = default)
    {
        try
        {
            await _svc.DeletePadraoAsync(codPadrao, ct);
            return Ok(new { success = true, message = "Padrão excluído com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{codPadrao:int}/vigente")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetVigente(int codPadrao, CancellationToken ct = default)
    {
        try
        {
            await _svc.SetVigenteAsync(codPadrao, User.GetCodUsuario(), ct);
            return Ok(new { success = true, message = "Padrão definido como vigente." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{codPadrao:int}/importar-referencia")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ImportarReferencia(
        int codPadrao,
        [FromBody] ImportarReferenciaPadraoRequest req,
        CancellationToken ct = default)
    {
        try
        {
            var data = await _svc.ImportarReferenciaAsync(codPadrao, req.CodReferencia, User.GetCodUsuario(), ct);
            return Ok(new { success = true, data });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{codPadrao:int}/faixas")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateFaixa(int codPadrao, [FromBody] UpsertFaixaPadraoRequest req, CancellationToken ct = default)
    {
        try
        {
            var codFaixa = await _svc.CreateFaixaAsync(codPadrao, req, User.GetCodUsuario(), ct);
            return Ok(new { success = true, codFaixa, message = "Faixa criada com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPut("faixas/{codFaixa:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateFaixa(int codFaixa, [FromBody] UpsertFaixaPadraoRequest req, CancellationToken ct = default)
    {
        try
        {
            await _svc.UpdateFaixaAsync(codFaixa, req, User.GetCodUsuario(), ct);
            return Ok(new { success = true, message = "Faixa atualizada com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("faixas/{codFaixa:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteFaixa(int codFaixa, CancellationToken ct = default)
    {
        try
        {
            await _svc.DeleteFaixaAsync(codFaixa, ct);
            return Ok(new { success = true, message = "Faixa excluída com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{codPadrao:int}/comentario")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpsertComentario(int codPadrao, [FromBody] ComentarioPadraoRequest req, CancellationToken ct = default)
    {
        try
        {
            await _svc.UpsertComentarioAsync(codPadrao, req.CodVariavel, req.Texto ?? "", User.GetCodUsuario(), ct);
            return Ok(new { success = true, message = "Comentário salvo com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}

public sealed class ImportarReferenciaPadraoRequest
{
    public int CodReferencia { get; set; }
}

public sealed class ComentarioPadraoRequest
{
    public int CodVariavel { get; set; }
    public string? Texto { get; set; }
}
