using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Avaliacoes;

public static class AvaliacaoSchema
{
    public static async Task MigrateAsync(IFirebirdConnectionFactory db, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='SCRIPT_AVALIACAO'") > 0)
        {
            await ValidateAsync(conn);
            return;
        }
        using var reader = new StreamReader(typeof(AvaliacaoSchema).Assembly.GetManifestResourceStream("script-avaliacoes.sql")!);
        var sql = await reader.ReadToEndAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            await conn.ExecuteAsync(new CommandDefinition(statement, transaction: tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    public static async Task ValidateAsync(System.Data.IDbConnection conn)
    {
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME IN ('SCRIPT_AVALIACAO','SCRIPT_AVALIACAO_EVENTO')");
        if (count != 2) throw new AvaliacaoException(503, "Central de Avaliacoes indisponivel: execute --migrate-evaluations antes de habilitar a integracao.");
    }
}
