using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Auth;

public static class PasswordRecoverySchema
{
    public static async Task MigrateAsync(IFirebirdConnectionFactory db, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='USUARIO' AND RDB$FIELD_NAME='EMAIL'", transaction: tx) == 0)
            await conn.ExecuteAsync("ALTER TABLE USUARIO ADD EMAIL VARCHAR(254) CHARACTER SET ASCII", transaction: tx);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$INDICES WHERE RDB$INDEX_NAME='UX_USUARIO_EMAIL'", transaction: tx) == 0)
            await conn.ExecuteAsync("CREATE UNIQUE INDEX UX_USUARIO_EMAIL ON USUARIO COMPUTED BY (CAST(LOWER(TRIM(EMAIL)) AS VARCHAR(254) CHARACTER SET ASCII))", transaction: tx);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='PASSWORD_RESET'", transaction: tx) == 0)
            await conn.ExecuteAsync(@"CREATE TABLE PASSWORD_RESET (
                TOKEN_HASH CHAR(64) NOT NULL PRIMARY KEY,
                CODUSUARIO INTEGER NOT NULL REFERENCES USUARIO(CODUSUARIO) ON DELETE CASCADE,
                CREATED_UTC TIMESTAMP NOT NULL,
                EXPIRES_UTC TIMESTAMP NOT NULL,
                USED_UTC TIMESTAMP)", transaction: tx);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$INDICES WHERE RDB$INDEX_NAME='IX_PASSWORD_RESET_USER'", transaction: tx) == 0)
            await conn.ExecuteAsync("CREATE INDEX IX_PASSWORD_RESET_USER ON PASSWORD_RESET (CODUSUARIO, CREATED_UTC)", transaction: tx);
        await tx.CommitAsync(ct);
    }
}
