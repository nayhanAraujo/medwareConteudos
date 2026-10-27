using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public static class ScriptCodigoUniversalSchema
{
    public static async Task MigrateAsync(IFirebirdConnectionFactory db, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        using var reader = new StreamReader(typeof(ScriptCodigoUniversalSchema).Assembly
            .GetManifestResourceStream("script-codigo-universal.sql")!);
        var sql = await reader.ReadToEndAsync(ct);
        // Separate commits make new metadata visible; every phase can be retried.
        foreach (var statement in sql.Split("-- NEXT", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            await conn.ExecuteAsync(new CommandDefinition(statement, cancellationToken: ct));
    }
}
