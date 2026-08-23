using System.Data.Common;
using System.Globalization;
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
    Task<IReadOnlyList<DashboardActivityItem>> GetActivityAsync(CancellationToken ct);
    Task<IReadOnlyList<DashboardReferenciasAnoItem>> GetReferenciasPorAnoAsync(CancellationToken ct);
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

public sealed record DashboardActivityItem(
    string User,
    string Action,
    string Entity,
    string Time,
    string Message,
    string Icon);

public sealed record DashboardReferenciasAnoItem(string Year, int Total);

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
            await Count("MODELOSMENSAGENS"),
            await Count("IMPRESSO"),
            await Count("USUARIO"));
    }

    public async Task<IReadOnlyList<DashboardActivityItem>> GetActivityAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var items = new List<(DateTime At, DashboardActivityItem Item)>();

        items.AddRange(await QuerySourceAsync(conn, @"
            SELECT FIRST 12
                COALESCE(h.USUARIO, 'sistema') AS USUARIO,
                h.TIPO_ALTERACAO AS ACAO,
                COALESCE(s.NOME, h.DESCRICAO, 'Script') AS ENTIDADE,
                h.DATA_ALTERACAO AS DATA_EVENTO
            FROM SCRIPTVERSAOHISTORICO h
            LEFT JOIN SCRIPTVERSOES sv ON sv.CODVERSAO = h.CODVERSAO_DESTINO
            LEFT JOIN SCRIPTLAUDO s ON s.CODSCRIPTLAUDO = sv.CODSCRIPTLAUDO
            ORDER BY h.DATA_ALTERACAO DESC",
            "code-square", "atualizou o Script"));

        items.AddRange(await QuerySourceAsync(conn, @"
            SELECT FIRST 12
                COALESCE(sv.USUARIO_RESPONSAVEL, 'sistema') AS USUARIO,
                'criou nova versão' AS ACAO,
                COALESCE(s.NOME, sv.NUMERO_VERSAO, 'Script') AS ENTIDADE,
                sv.DATA_CRIACAO AS DATA_EVENTO
            FROM SCRIPTVERSOES sv
            LEFT JOIN SCRIPTLAUDO s ON s.CODSCRIPTLAUDO = sv.CODSCRIPTLAUDO
            ORDER BY sv.DATA_CRIACAO DESC",
            "layers", "criou nova versão"));

        items.AddRange(await QuerySourceAsync(conn, @"
            SELECT FIRST 12
                COALESCE(l.RESPONSAVEL, 'sistema') AS USUARIO,
                l.TIPOALTERACAO AS ACAO,
                COALESCE(p.NOME, l.DESCRICAOALTERACAO, 'Painel') AS ENTIDADE,
                l.DATAALTERACAO AS DATA_EVENTO
            FROM LOGALTERACOES l
            LEFT JOIN VersoesPainel v ON v.CODVERSAOPAINEL = l.CODVERSAOPAINEL
            LEFT JOIN Paineis p ON p.CODPAINEL = v.CODPAINEL
            ORDER BY l.DATAALTERACAO DESC",
            "bar-chart-fill", "atualizou o Painel"));

        return items
            .OrderByDescending(x => x.At)
            .Take(12)
            .Select(x => x.Item)
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardReferenciasAnoItem>> GetReferenciasPorAnoAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        try
        {
            var rows = await conn.QueryAsync(@"
                SELECT r.ANO AS ANO, COUNT(*) AS TOTAL
                FROM REFERENCIA r
                GROUP BY r.ANO
                ORDER BY r.ANO NULLS LAST");

            return rows.Select(row =>
            {
                var d = (IDictionary<string, object>)row;
                var year = FormatYear(d["ANO"]);
                var total = Convert.ToInt32(d["TOTAL"]);
                return new DashboardReferenciasAnoItem(year, total);
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string FormatYear(object? value)
    {
        if (value is null or DBNull) return "Sem ano";
        if (value is short or int or long)
        {
            var year = Convert.ToInt32(value);
            return year > 0 ? year.ToString(CultureInfo.InvariantCulture) : "Sem ano";
        }
        var text = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? "Sem ano" : text;
    }

    private static async Task<IReadOnlyList<(DateTime At, DashboardActivityItem Item)>> QuerySourceAsync(
        DbConnection conn, string sql, string icon, string fallbackAction)
    {
        try
        {
            var rows = await conn.QueryAsync(sql);
            var result = new List<(DateTime At, DashboardActivityItem Item)>();
            foreach (var row in rows)
            {
                var d = (IDictionary<string, object>)row;
                var user = d["USUARIO"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(user)) user = "sistema";
                var action = HumanizeAction(d["ACAO"]?.ToString(), fallbackAction);
                var entity = d["ENTIDADE"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(entity)) entity = "registro";
                var at = d["DATA_EVENTO"] as DateTime? ?? DateTime.Now;
                var time = at.ToString("HH:mm:ss", CultureInfo.GetCultureInfo("pt-BR"));
                var message = $"Utilizador {user} {action} '{entity}' — {time}";
                result.Add((at, new DashboardActivityItem(user, action, entity, time, message, icon)));
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static string HumanizeAction(string? tipo, string fallback)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return fallback;
        var raw = tipo.Trim();
        if (raw.Contains(' ')) return raw;
        return raw.ToLowerInvariant() switch
        {
            "create" or "criacao" or "criar" or "insert" => "criou",
            "update" or "alteracao" or "editar" or "edit" => "atualizou",
            "delete" or "exclusao" or "remover" => "removeu",
            "aprovacao" or "aprovado" or "approve" => "aprovou",
            _ => fallback
        };
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

    [HttpGet("activity")]
    public async Task<IActionResult> Activity(CancellationToken ct)
        => Ok(ApiResponse.Ok(await service.GetActivityAsync(ct)));

    [HttpGet("referencias-por-ano")]
    public async Task<IActionResult> ReferenciasPorAno(CancellationToken ct)
        => Ok(ApiResponse.Ok(await service.GetReferenciasPorAnoAsync(ct)));
}
