using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/normalidades/converter-unidade")]
[Authorize]
public class NormalidadeUnidadeConversaoWebController : ControllerBase
{
    private readonly INormalidadeUnidadeConversaoService _svc;

    public NormalidadeUnidadeConversaoWebController(INormalidadeUnidadeConversaoService svc) => _svc = svc;

    [HttpGet("pares")]
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Visualizar)]
    public IActionResult ListPares() =>
        Ok(new { success = true, data = _svc.ListParesSuportados() });

    [HttpPost("preview")]
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Editar)]
    public async Task<IActionResult> Preview([FromBody] ConversaoUnidadeRequest req, CancellationToken ct = default)
    {
        try
        {
            var data = await _svc.PreviewAsync(req, ct);
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

    [HttpPost("apply")]
    [RequirePermission(PermissionDomains.Variaveis, PermissionActions.Editar)]
    public async Task<IActionResult> Apply([FromBody] ConversaoUnidadeApplyRequest req, CancellationToken ct = default)
    {
        try
        {
            var data = await _svc.ApplyAsync(req, User.GetCodUsuario(), ct);
            return Ok(new { success = true, data, message = data.Message });
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
