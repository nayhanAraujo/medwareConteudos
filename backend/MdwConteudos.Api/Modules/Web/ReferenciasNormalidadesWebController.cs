using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/variaveis/referencias-normalidades")]
[Authorize]
public class ReferenciasNormalidadesWebController : ControllerBase
{
    private readonly IReferenciasService _svc;
    private readonly INormalidadesJsonImportService _jsonImport;

    public ReferenciasNormalidadesWebController(IReferenciasService svc, INormalidadesJsonImportService jsonImport)
    {
        _svc = svc;
        _jsonImport = jsonImport;
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

    [HttpGet("por-variavel")]
    public async Task<IActionResult> GetPorVariavel(
        [FromQuery] int? variavelId = null,
        [FromQuery] string? busca = null,
        [FromQuery] int limite = 100,
        CancellationToken ct = default
    )
    {
        try
        {
            var data = await _svc.GetNormalidadesPorVariavelAsync(variavelId, busca, limite, ct);
            return Ok(new { success = true, data });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("vincular")]
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Vincular)]
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
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Editar)]
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
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Vincular)]
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
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Importar)]
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

    [HttpPost("comentario")]
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Editar)]
    public async Task<IActionResult> UpsertComentario([FromBody] UpsertNormalidadeComentarioRequest req, CancellationToken ct = default)
    {
        try
        {
            await _svc.UpsertNormalidadeComentarioAsync(
                req.CodVariavel,
                req.CodReferencia,
                req.Texto,
                User.GetCodUsuario(),
                ct,
                req.Sexo,
                req.IdadeMin,
                req.IdadeMax
            );
            return Ok(new { success = true, message = string.IsNullOrWhiteSpace(req.Texto) ? "Comentário removido." : "Comentário salvo." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("comentario")]
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Excluir)]
    public async Task<IActionResult> DeleteComentario(
        [FromQuery] int codVariavel,
        [FromQuery] int codReferencia,
        [FromQuery] string? sexo = null,
        [FromQuery] int? idadeMin = null,
        [FromQuery] int? idadeMax = null,
        CancellationToken ct = default
    )
    {
        try
        {
            await _svc.DeleteNormalidadeComentarioAsync(codVariavel, codReferencia, ct, sexo, idadeMin, idadeMax);
            return Ok(new { success = true, message = "Comentário removido." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("importar-json")]
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Importar)]
    public async Task<IActionResult> ImportarJson([FromBody] ImportarNormalidadesJsonRequest? req, CancellationToken ct = default)
    {
        try
        {
            var data = await _jsonImport.ImportarAseChamberAsync(req?.CodReferencia > 0 ? req.CodReferencia : 1, User.GetCodUsuario(), ct);
            return Ok(new { success = true, data });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }
}
