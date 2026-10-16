using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public static class ModuloRelatorioIcone
{
    public static async Task MigrateAsync(IFirebirdConnectionFactory db, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='MODULORELATORIO' AND RDB$FIELD_NAME='ICONE_BASE64'", cancellationToken: ct));
        if (exists == 0)
            await connection.ExecuteAsync(new CommandDefinition("ALTER TABLE MODULORELATORIO ADD ICONE_BASE64 BLOB SUB_TYPE TEXT CHARACTER SET UTF8", cancellationToken: ct));
    }
}
