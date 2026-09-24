using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Auth;

[ApiController]
[Route("api/web/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IPermissionService _permissions;

    public AuthController(IAuthService auth, IPermissionService permissions)
    {
        _auth = auth;
        _permissions = permissions;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(req.Usuario, req.Senha, ct);
        if (!result.Success)
            return Unauthorized(new { success = false, message = result.Error ?? "Usuário ou senha inválidos." });
        var u = (SessionUser)result.User!;
        var permissions = await _permissions.GetEffectivePermissionsAsync(u.CodUsuario, u.Role, ct);
        return Ok(new
        {
            success = true,
            token = result.Token,
            user = new { codusuario = u.CodUsuario, nome = u.Nome, role = u.Role, permissions }
        });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        return Task.FromResult<IActionResult>(StatusCode(410,
            ApiResponse.Fail("Indisponível", "Solicite a redefinição da senha a um administrador.", 410)));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var nome = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var codUsuario = int.TryParse(id, out var parsed) ? parsed : 0;
        IEnumerable<string> permissions = codUsuario > 0
            ? await _permissions.GetEffectivePermissionsAsync(codUsuario, role ?? "", ct)
            : Array.Empty<string>();
        return Ok(new { success = true, user = new { codusuario = codUsuario, nome, role, permissions } });
    }

    [HttpGet("permissoes")]
    [Authorize]
    public async Task<IActionResult> Permissoes(CancellationToken ct)
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        if (!int.TryParse(id, out var codUsuario))
            return Unauthorized(ApiResponse.Fail("Não autenticado", "Sessão inválida.", 401));
        return Ok(ApiResponse.Ok(await _permissions.GetEffectivePermissionsAsync(codUsuario, role, ct)));
    }
}
