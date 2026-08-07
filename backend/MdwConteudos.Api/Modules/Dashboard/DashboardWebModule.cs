using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Dashboard;

public static class DashboardModuleRegistration
{
    public static IServiceCollection AddDashboardModule(this IServiceCollection services)
        => services.AddScoped<IDashboardService, DashboardService>();
}

public interface IDashboardService
{
    Task<DashboardStats> GetStatsAsync(CancellationToken ct);
}

public sealed record DashboardStats(
    int Variaveis,
    int Referencias,
    int Formulas,
    int Scripts,
    int Relatorios,
    int Paineis,
    int ModelosMensagens,
    int Impressos,
    int Usuarios);

public sealed class DashboardService(IFirebirdConnectionFactory db) : IDashboardService
{
    public async Task<DashboardStats> GetStatsAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        async Task<int> Count(string table)
        {
            try
            {
                return await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table}");
            }
            catch
            {
                return 0;
            }
        }

        return new DashboardStats(
            await Count("VARIAVEIS"),
            await Count("REFERENCIA"),
            await Count("FORMULAS"),
            await Count("SCRIPTLAUDO"),
            await Count("RELATORIOS"),
            await Count("PAINEIS"),
            await Count("MODELOS_MENSAGENS"),
            await Count("IMPRESSO"),
            await Count("USUARIO"));
    }
}

[ApiController]
[Route("api/web/dashboard")]
[Authorize]
public sealed class DashboardController(IDashboardService service) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
        => Ok(ApiResponse.Ok(await service.GetStatsAsync(ct)));
}
