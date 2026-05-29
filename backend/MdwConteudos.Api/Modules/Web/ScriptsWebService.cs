using Dapper;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public interface IScriptsWebService
{
    Task<object> ListAsync(string? sistema, int? ativo, CancellationToken ct);
}

public class ScriptsWebService : IScriptsWebService
{
    private readonly IFirebirdConnectionFactory _db;
    public ScriptsWebService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<object> ListAsync(string? sistema, int? ativo, CancellationToken ct)
    {
        var where = new List<string> { "1=1" };
        var p = new DynamicParameters();
        if (!string.IsNullOrEmpty(sistema)) { where.Add("SISTEMA = @s"); p.Add("s", sistema); }
        if (ativo.HasValue) { where.Add("ATIVO = @a"); p.Add("a", ativo); }
        var sql = $"SELECT CODSCRIPTLAUDO, NOME, SISTEMA, APROVADO, ATIVO FROM SCRIPTLAUDO WHERE {string.Join(" AND ", where)} ORDER BY NOME";
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync(sql, p);
        return ApiResponse.Ok(rows, rows.Count());
    }
}

