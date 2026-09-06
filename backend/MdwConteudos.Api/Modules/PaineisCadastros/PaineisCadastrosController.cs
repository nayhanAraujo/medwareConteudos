using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.PaineisCadastros;

[ApiController]
[Authorize]
[Route("api/web/paineis/cadastros")]
public sealed class PaineisCadastrosController(IPaineisCadastrosService service) : ControllerBase
{
    [HttpGet("clientes")]
    public Task<IActionResult> ListClientes(CancellationToken ct) => List(PainelCadastroTipo.Cliente, ct);

    [HttpPost("clientes"), RequirePermission(PermissionDomains.Paineis, PermissionActions.Criar)]
    public Task<IActionResult> CreateCliente(PainelCadastroRequest request, CancellationToken ct) => Create(PainelCadastroTipo.Cliente, request, "Cliente cadastrado.", ct);

    [HttpPut("clientes/{id:int}"), RequirePermission(PermissionDomains.Paineis, PermissionActions.Editar)]
    public Task<IActionResult> UpdateCliente(int id, PainelCadastroRequest request, CancellationToken ct) => Update(PainelCadastroTipo.Cliente, id, request, "Cliente atualizado.", ct);

    [HttpDelete("clientes/{id:int}"), RequirePermission(PermissionDomains.Paineis, PermissionActions.Excluir)]
    public Task<IActionResult> DeleteCliente(int id, CancellationToken ct) => Delete(PainelCadastroTipo.Cliente, id, "Cliente excluído.", ct);

    [HttpGet("modulos")]
    public Task<IActionResult> ListModulos(CancellationToken ct) => List(PainelCadastroTipo.Modulo, ct);

    [HttpPost("modulos"), RequirePermission(PermissionDomains.Paineis, PermissionActions.Criar)]
    public Task<IActionResult> CreateModulo(PainelCadastroRequest request, CancellationToken ct) => Create(PainelCadastroTipo.Modulo, request, "Módulo cadastrado.", ct);

    [HttpPut("modulos/{id:int}"), RequirePermission(PermissionDomains.Paineis, PermissionActions.Editar)]
    public Task<IActionResult> UpdateModulo(int id, PainelCadastroRequest request, CancellationToken ct) => Update(PainelCadastroTipo.Modulo, id, request, "Módulo atualizado.", ct);

    [HttpDelete("modulos/{id:int}"), RequirePermission(PermissionDomains.Paineis, PermissionActions.Excluir)]
    public Task<IActionResult> DeleteModulo(int id, CancellationToken ct) => Delete(PainelCadastroTipo.Modulo, id, "Módulo excluído.", ct);

    [HttpGet("pacotes-comerciais")]
    public Task<IActionResult> ListPacotes(CancellationToken ct) => List(PainelCadastroTipo.PacoteComercial, ct);

    [HttpPost("pacotes-comerciais"), RequirePermission(PermissionDomains.Paineis, PermissionActions.Criar)]
    public Task<IActionResult> CreatePacote(PainelCadastroRequest request, CancellationToken ct) => Create(PainelCadastroTipo.PacoteComercial, request, "Pacote comercial cadastrado.", ct);

    [HttpPut("pacotes-comerciais/{id:int}"), RequirePermission(PermissionDomains.Paineis, PermissionActions.Editar)]
    public Task<IActionResult> UpdatePacote(int id, PainelCadastroRequest request, CancellationToken ct) => Update(PainelCadastroTipo.PacoteComercial, id, request, "Pacote comercial atualizado.", ct);

    [HttpDelete("pacotes-comerciais/{id:int}"), RequirePermission(PermissionDomains.Paineis, PermissionActions.Excluir)]
    public Task<IActionResult> DeletePacote(int id, CancellationToken ct) => Delete(PainelCadastroTipo.PacoteComercial, id, "Pacote comercial excluído.", ct);

    private async Task<IActionResult> List(PainelCadastroTipo type, CancellationToken ct)
    {
        var rows = await service.ListAsync(type, ct);
        return Ok(ApiResponse.Ok(rows, rows.Count));
    }

    private async Task<IActionResult> Create(PainelCadastroTipo type, PainelCadastroRequest request, string message, CancellationToken ct)
    {
        try
        {
            await service.CreateAsync(type, request, ct);
            return Ok(ApiResponse.OkMessage(message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail("Validação", ex.Message));
        }
    }

    private async Task<IActionResult> Update(PainelCadastroTipo type, int id, PainelCadastroRequest request, string message, CancellationToken ct)
    {
        try
        {
            return await service.UpdateAsync(type, id, request, ct)
                ? Ok(ApiResponse.OkMessage(message))
                : NotFound(ApiResponse.Fail("Registro não encontrado"));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail("Validação", ex.Message));
        }
    }

    private async Task<IActionResult> Delete(PainelCadastroTipo type, int id, string message, CancellationToken ct)
    {
        try
        {
            return await service.DeleteAsync(type, id, ct)
                ? Ok(ApiResponse.OkMessage(message))
                : NotFound(ApiResponse.Fail("Registro não encontrado"));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail("Validação", ex.Message));
        }
    }
}
