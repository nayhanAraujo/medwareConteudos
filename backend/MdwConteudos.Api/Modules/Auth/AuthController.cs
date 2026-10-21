using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Auth;

[ApiController]
[Route("api/web/auth")]
public class AuthController : ControllerBase
{
    private readonly IPasswordRecoveryService _recovery;
    private readonly IAuthService _auth;
    private readonly IPermissionService _permissions;

    public AuthController(IAuthService auth, IPermissionService permissions, IPasswordRecoveryService recovery)
    {
        _recovery = recovery;
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

    /// <summary>Envia um link de recuperação para o e-mail cadastrado, sem revelar a existência da conta.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        var email = RecoveryEmail.Normalize(req.Email);
        if (!RecoveryEmail.IsValid(email)) return BadRequest(ApiResponse.Fail("E-mail inválido", "Informe um e-mail válido."));
        await _recovery.RequestAsync(email!, ct);
        return Ok(ApiResponse.OkMessage(PasswordRecoveryService.Confirmation));
    }

    /// <summary>Redefine a senha usando um token de uso único com validade de 30 minutos.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            return BadRequest(ApiResponse.Fail("Senha inválida", "Senha deve ter no mínimo 6 caracteres."));
        if (req.NewPassword != req.ConfirmPassword)
            return BadRequest(ApiResponse.Fail("Senha inválida", "As senhas não coincidem."));
        if (string.IsNullOrEmpty(req.Token) || !await _recovery.ResetAsync(req.Token, req.NewPassword, ct))
            return BadRequest(ApiResponse.Fail("Link inválido", PasswordRecoveryService.InvalidLink));
        return Ok(ApiResponse.OkMessage("Senha redefinida. Entre com sua nova senha."));
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
