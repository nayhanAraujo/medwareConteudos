using System.Data.Common;
using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Orcamentos;

public static class OrcamentoEmailSourceSchema
{
    // One satellite per quote, not an N:N relation. The source's full identifiers live in
    // a private UTF8 BLOB; fixed hashes keep unique indexes small and independent of collation.
    public const string Definition = "CODORCAMENTO BIGINT NOT NULL, CHAVEORIGEM CHAR(64) CHARACTER SET ASCII NOT NULL, DOCUMENTO BLOB SUB_TYPE TEXT CHARACTER SET UTF8 NOT NULL, DTHRCRIACAO TIMESTAMP NOT NULL, CONSTRAINT PK_ORCAMENTOEMAIL PRIMARY KEY(CODORCAMENTO), CONSTRAINT UNQ_ORCAMENTOEMAIL_CHAVEORIGEM UNIQUE(CHAVEORIGEM), CONSTRAINT FK_ORC_EMAIL_ORC FOREIGN KEY(CODORCAMENTO) REFERENCES ORCAMENTO(CODORCAMENTO)";
    public static async Task Migrate(IFirebirdConnectionFactory db, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct);
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='ORCAMENTOEMAIL'", cancellationToken: ct)) == 0)
            await c.ExecuteAsync(new CommandDefinition($"CREATE TABLE ORCAMENTOEMAIL({Definition})", cancellationToken: ct));
    }
    public static async Task Validate(DbConnection c, CancellationToken ct)
    {
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='ORCAMENTOEMAIL' AND RDB$FIELD_NAME IN ('CODORCAMENTO','CHAVEORIGEM','DOCUMENTO','DTHRCRIACAO')", cancellationToken: ct)) != 4
            || await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$RELATION_NAME='ORCAMENTOEMAIL' AND RDB$CONSTRAINT_NAME IN ('PK_ORCAMENTOEMAIL','UNQ_ORCAMENTOEMAIL_CHAVEORIGEM','FK_ORC_EMAIL_ORC')", cancellationToken: ct)) != 3)
            throw new InvalidOperationException("Origem de e-mail dos orçamentos não preparada. Execute --migrate-database antes de iniciar esta versão.");
    }
}
