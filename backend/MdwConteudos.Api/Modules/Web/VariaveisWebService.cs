using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public interface IVariaveisWebService
{
    Task<object> ListVariaveisAsync(int skip, int take, string? search, CancellationToken ct);
    Task<object?> GetVariavelAsync(int id, CancellationToken ct);
}

public class VariaveisWebService : IVariaveisWebService
{
    private readonly IFirebirdConnectionFactory _db;
    public VariaveisWebService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<object> ListVariaveisAsync(int skip, int take, string? search, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var sql = @"SELECT FIRST @take SKIP @skip CODVARIAVEL, NOME, VARIAVEL, SIGLA FROM VARIAVEIS";
        if (!string.IsNullOrWhiteSpace(search))
            sql += " WHERE UPPER(NOME) LIKE UPPER(@s) OR UPPER(VARIAVEL) LIKE UPPER(@s)";
        sql += " ORDER BY NOME";
        var rows = await conn.QueryAsync(sql, new { take, skip, s = $"%{search}%" });
        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VARIAVEIS");
        return ApiResponse.Ok(rows, total);
    }

    public async Task<object?> GetVariavelAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync("SELECT * FROM VARIAVEIS WHERE CODVARIAVEL = @id", new { id });
    }
}

