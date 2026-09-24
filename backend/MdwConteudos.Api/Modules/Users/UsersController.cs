using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;

namespace MdwConteudos.Api.Modules.Users;

[ApiController]
[Route("api/web/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUsersService _users;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUsersService users, ILogger<UsersController>? logger = null)
    {
        _users = users;
        _logger = logger ?? NullLogger<UsersController>.Instance;
    }

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private async Task<bool> IsCurrentAdmin(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true || ActorId <= 0 ||
            !AccountSecurity.IsAdmin(User.FindFirstValue(ClaimTypes.Role))) return false;
        var actor = await _users.GetAsync(ActorId, ct);
        return actor is { Status: -1 } && AccountSecurity.IsAdmin(actor.Perfil);
    }

    private void AuditReset(int target, string outcome) =>
        _logger.LogInformation("AccountPasswordReset Actor={ActorId} Target={TargetId} Outcome={Outcome}", ActorId, target, outcome);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var data = await _users.ListAsync(ct);
        return Ok(ApiResponse.Ok(data, data.Count()));
    }

    [HttpGet("{codUsuario:int}")]
    public async Task<IActionResult> Get(int codUsuario, CancellationToken ct)
    {
        var u = await _users.GetAsync(codUsuario, ct);
        if (u is null) return NotFound(ApiResponse.Fail("Não encontrado"));
        return Ok(ApiResponse.Ok(u));
    }

    [HttpPost]
    [RequirePermission(PermissionDomains.Usuarios, PermissionActions.Criar)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (AccountSecurity.IsAdmin(req.Perfil) && !await IsCurrentAdmin(ct)) return Forbid();
        var (ok, error) = await _users.CreateAsync(req, ct);
        if (!ok) return BadRequest(ApiResponse.Fail("Erro", error));
        return Ok(ApiResponse.OkMessage("Usuário cadastrado."));
    }

    [HttpPut("{codUsuario:int}")]
    [RequirePermission(PermissionDomains.Usuarios, PermissionActions.Editar)]
    public async Task<IActionResult> Update(int codUsuario, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var resetting = !string.IsNullOrEmpty(req.Senha) || !string.IsNullOrEmpty(req.ConfirmarSenha);
        var outcome = "failed";
        try
        {
            var target = await _users.GetAsync(codUsuario, ct);
            if (target is null) { outcome = "not_found"; return NotFound(ApiResponse.Fail("Não encontrado")); }
            if ((AccountSecurity.IsAdmin(target.Perfil) || AccountSecurity.IsAdmin(req.Perfil) ||
                 (resetting && codUsuario != ActorId)) && !await IsCurrentAdmin(ct))
            {
                outcome = "denied";
                return Forbid();
            }
            var (ok, error) = await _users.UpdateAsync(codUsuario, req, ct);
            outcome = ok ? "success" : "rejected";
            if (!ok) return BadRequest(ApiResponse.Fail("Erro", error));
            return Ok(ApiResponse.OkMessage("Usuário atualizado."));
        }
        finally
        {
            if (resetting) AuditReset(codUsuario, outcome);
        }
    }

    [HttpDelete("{codUsuario:int}")]
    [RequirePermission(PermissionDomains.Usuarios, PermissionActions.Excluir)]
    public async Task<IActionResult> Delete(int codUsuario, CancellationToken ct)
    {
        var target = await _users.GetAsync(codUsuario, ct);
        if (target is null) return NotFound(ApiResponse.Fail("Não encontrado"));
        if (AccountSecurity.IsAdmin(target.Perfil) && !await IsCurrentAdmin(ct)) return Forbid();
        if (!await _users.DeleteAsync(codUsuario, ct))
            return NotFound(ApiResponse.Fail("Não encontrado"));
        return Ok(ApiResponse.OkMessage("Usuário excluído."));
    }
}
