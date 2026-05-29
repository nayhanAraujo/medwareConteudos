using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Auth;

[ApiController]
[Route("api/web/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(req.Usuario, req.Senha, ct);
        if (!result.Success)
            return Unauthorized(new { success = false, message = result.Error ?? "Usuário ou senha inválidos." });
        var u = (SessionUser)result.User!;
        return Ok(new
        {
            success = true,
            token = result.Token,
            user = new { codusuario = u.CodUsuario, nome = u.Nome, role = u.Role }
        });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        if (req.NewPassword != req.ConfirmPassword)
            return BadRequest(ApiResponse.Fail("Validação", "As senhas não coincidem."));
        var (ok, error) = await _auth.ForgotPasswordAsync(req.Identificacao, req.NewPassword, ct);
        if (!ok) return BadRequest(ApiResponse.Fail("Erro", error));
        return Ok(ApiResponse.OkMessage("Senha alterada com sucesso."));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var nome = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        return Ok(new { success = true, user = new { codusuario = id, nome, role } });
    }
}
