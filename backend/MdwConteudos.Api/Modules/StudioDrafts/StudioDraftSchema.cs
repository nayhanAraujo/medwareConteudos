using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public static class StudioDraftSchema
{
    public static async Task MigrateAsync(IFirebirdConnectionFactory db, CancellationToken ct)
    {
        using var reader = new StreamReader(typeof(StudioDraftSchema).Assembly.GetManifestResourceStream("studio-rascunhos.sql")!);
        var sql = await reader.ReadToEndAsync(ct);
        await using var c = await db.OpenConnectionAsync(ct);
        // Commit DDL before seed DML: Firebird metadata visibility requires this boundary.
        using (var tx = c.BeginTransaction())
        {
            foreach (var part in sql.Split("-- @statement").Skip(1))
            {
                var statement = part.Trim().TrimEnd(';');
                var words = statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length < 3) continue;
                if (words[0] == "CREATE" && words[1] is "TABLE" or "INDEX")
                {
                    var table = words[1] == "TABLE" ? "RDB$RELATIONS" : "RDB$INDICES";
                    var field = words[1] == "TABLE" ? "RDB$RELATION_NAME" : "RDB$INDEX_NAME";
                    if (await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM {table} WHERE {field}=@name", new { name = words[2] }, tx, cancellationToken: ct)) > 0) continue;
                }
                await c.ExecuteAsync(new CommandDefinition(statement, transaction: tx, cancellationToken: ct));
            }
            tx.Commit();
        }
        using (var tx = c.BeginTransaction())
        {
            await c.ExecuteAsync(new CommandDefinition("INSERT INTO STUDIOMODELOESTADO(CODMODELO,VERSAO) SELECT M.CODMODELO,0 FROM MODELOMODOTEXTO M WHERE NOT EXISTS(SELECT 1 FROM STUDIOMODELOESTADO S WHERE S.CODMODELO=M.CODMODELO)", transaction: tx, cancellationToken: ct));
            tx.Commit();
        }
        foreach (var name in new[] { "PK_STUDIORASCUNHO", "PK_STUDIOREVISAO", "UNQ_SV_COMANDO", "FK_SR_CODUSUARIO", "FK_SV_CODRASCUNHO", "PK_STUDIOMODELOESTADO", "PK_STUDIOPUBLICACAO" })
            if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$CONSTRAINT_NAME=@name", new { name }, cancellationToken: ct)) != 1)
                throw new InvalidOperationException("Esquema Studio incompatível: " + name);
    }
}
