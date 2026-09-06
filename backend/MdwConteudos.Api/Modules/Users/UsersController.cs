using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Users;

[ApiController]
[Route("api/web/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUsersService _users;

    public UsersController(IUsersService users) => _users = users;

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
        var (ok, error) = await _users.CreateAsync(req, ct);
        if (!ok) return BadRequest(ApiResponse.Fail("Erro", error));
        return Ok(ApiResponse.OkMessage("Usuário cadastrado."));
    }

    [HttpPut("{codUsuario:int}")]
    [RequirePermission(PermissionDomains.Usuarios, PermissionActions.Editar)]
    public async Task<IActionResult> Update(int codUsuario, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var (ok, error) = await _users.UpdateAsync(codUsuario, req, ct);
        if (!ok) return BadRequest(ApiResponse.Fail("Erro", error));
        return Ok(ApiResponse.OkMessage("Usuário atualizado."));
    }

    [HttpDelete("{codUsuario:int}")]
    [RequirePermission(PermissionDomains.Usuarios, PermissionActions.Excluir)]
    public async Task<IActionResult> Delete(int codUsuario, CancellationToken ct)
    {
        if (!await _users.DeleteAsync(codUsuario, ct))
            return NotFound(ApiResponse.Fail("Não encontrado"));
        return Ok(ApiResponse.OkMessage("Usuário excluído."));
    }
}
