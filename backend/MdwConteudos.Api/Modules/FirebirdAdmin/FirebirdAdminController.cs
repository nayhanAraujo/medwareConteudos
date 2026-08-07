using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.FirebirdAdmin;

[ApiController]
[Route("api/web/firebird-admin")]
[Authorize(Roles = "admin")]
public sealed class FirebirdAdminController(
    IFirebirdAdminService service,
    ILogger<FirebirdAdminController> logger) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(ApiResponse.Ok(service.GetStatus()));

    [HttpPost("testar")]
    public Task<IActionResult> Testar(FirebirdConnectionRequest request, CancellationToken ct)
        => Execute(() => service.TestConnectionAsync(request, ct), "testar a conexão Firebird");

    [HttpPost("consulta-teste")]
    public Task<IActionResult> ConsultaTeste(FirebirdConnectionContextRequest request, CancellationToken ct)
        => Execute(() => service.RunProbeAsync(request.Connection, ct), "executar a consulta de teste");

    [HttpPost("sql")]
    public Task<IActionResult> Sql(FirebirdSqlRequest request, CancellationToken ct)
        => Execute(() => service.ExecuteSqlAsync(request, ct), "executar a consulta SQL");

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action, string operation)
    {
        try
        {
            return Ok(ApiResponse.Ok(await action()));
        }
        catch (FirebirdAdminValidationException ex)
        {
            return BadRequest(ApiResponse.Fail("Requisição inválida.", ex.Message));
        }
        catch (FirebirdAdminConnectionException ex)
        {
            logger.LogWarning(ex, "Falha administrativa ao {Operation}", operation);
            return BadRequest(ApiResponse.Fail("Falha de conexão.", ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro administrativo ao {Operation}", operation);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("Erro interno do servidor.", "A operação Firebird não pôde ser concluída.", 500));
        }
    }
}

