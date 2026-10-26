using System.Text.RegularExpressions;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Orcamentos;

namespace ConversorHtml.Tests;

public sealed class OrcamentoNamingMigrationTests
{
    private static string ScriptPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var file = Path.Combine(dir.FullName, "docs", "sql", "scripts", "normalize-orcamentos-names.sql");
            if (File.Exists(file)) return file;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Script versionado de correção dos nomes não encontrado.");
    }
    private static string Script() => File.ReadAllText(ScriptPath());

    [FirebirdPublicationFact]
    public async Task ScriptRecreatesEmptySchemaPreservesSeedAndRejectsDataAndRepeatedExecution()
    {
        var cs = new FbConnectionStringBuilder { Database = Path.Combine(Path.GetTempPath(), $"quote-names-{Guid.NewGuid():N}.fdb"), ServerType = FbServerType.Embedded,
            ClientLibrary = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"), UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false }.ToString();
        FbConnection.CreateDatabase(cs, overwrite: false);
        try
        {
            var factory = new Factory(cs); await using var c = await factory.OpenConnectionAsync();
            await c.ExecuteAsync("CREATE TABLE CLIENTES(CODCLIENTE INTEGER PRIMARY KEY)");
            await c.ExecuteAsync("CREATE TABLE ORCAMENTO(ID BIGINT PRIMARY KEY)");
            foreach (var name in new[] { "CLIENTE_COMERCIAL", "ORCAMENTO_PROPOSTA", "ORCAMENTO_ANEXO", "ORCAMENTO_ITEM", "ORCAMENTO_EDITOR", "ORCAMENTO_EVENTO", "ORCAMENTO_REVISAO" })
                await c.ExecuteAsync($"CREATE TABLE {name}(ORCAMENTO_ID BIGINT REFERENCES ORCAMENTO(ID))");
            await c.ExecuteAsync("CREATE TABLE ORCAMENTO_POLITICA(ID BIGINT PRIMARY KEY,DOCUMENTO BLOB SUB_TYPE TEXT CHARACTER SET UTF8,CODUSUARIO INTEGER,MOTIVO VARCHAR(1000) CHARACTER SET UTF8,CRIADO_UTC TIMESTAMP)");
            var document = OrcamentoJson.Write(QuotePolicy.Initial);
            await c.ExecuteAsync("INSERT INTO ORCAMENTO_POLITICA VALUES(1,@document,0,'Referências iniciais do POP-GO-001 v1.0; nenhuma condição comercial presumida',CURRENT_TIMESTAMP)", new { document });
            await c.ExecuteAsync("CREATE TABLE ORCAMENTO_CONFIG(ID INTEGER PRIMARY KEY,VERSION BIGINT)");
            await c.ExecuteAsync("INSERT INTO ORCAMENTO_CONFIG VALUES(1,1)");
            var preflight = await Assert.ThrowsAsync<InvalidOperationException>(() => OrcamentoSchema.MigrateAsync(factory));
            Assert.Contains("normalize-orcamentos-names.sql", preflight.Message);
            await c.ExecuteAsync("CREATE EXCEPTION EX_ORC_NOMES 'recusado'");
            var block = Regex.Match(Script(), @"EXECUTE BLOCK AS[\s\S]*?END\^").Value.TrimEnd('^');
            Assert.NotEmpty(block);
            await c.ExecuteAsync("INSERT INTO ORCAMENTO VALUES(99)");
            await using (var tx = await c.BeginTransactionAsync())
            {
                await Assert.ThrowsAsync<FbException>(() => c.ExecuteAsync(new CommandDefinition(block, transaction: tx)));
                await tx.RollbackAsync();
            }
            Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ORCAMENTO WHERE ID=99"));
            Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='ORCAMENTOANEXO'"));
            await c.ExecuteAsync("DELETE FROM ORCAMENTO WHERE ID=99");
            await c.ExecuteAsync("UPDATE ORCAMENTO_POLITICA SET CODUSUARIO=2");
            await Assert.ThrowsAsync<FbException>(() => c.ExecuteAsync(block));
            await c.ExecuteAsync("UPDATE ORCAMENTO_POLITICA SET CODUSUARIO=0");
            var isql = Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"))!, OperatingSystem.IsWindows() ? "isql.exe" : "isql");
            if (File.Exists(isql))
            {
                await c.ExecuteAsync("DROP EXCEPTION EX_ORC_NOMES"); await c.CloseAsync();
                var start = new System.Diagnostics.ProcessStartInfo(isql) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { "-b", "-ch", "UTF8", "-user", "SYSDBA", "-password", "masterkey", "-input", ScriptPath(), new FbConnectionStringBuilder(cs).Database }) start.ArgumentList.Add(argument);
                using var process = System.Diagnostics.Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try { await process.WaitForExitAsync(timeout.Token); } catch { if (!process.HasExited) process.Kill(); throw; }
                var output = await stdout + await stderr;
                Assert.True(process.ExitCode == 0, output);
                await c.OpenAsync(); await c.ExecuteAsync("CREATE EXCEPTION EX_ORC_NOMES 'recusado'");
            }
            else
            {
            var scriptSql = Script();
            var following = scriptSql[(scriptSql.IndexOf("SET TERM ; ^", StringComparison.Ordinal) + "SET TERM ; ^".Length)..];
            following = following[..following.IndexOf("DROP EXCEPTION", StringComparison.Ordinal)];
            following = Regex.Replace(following, @"(?m)^--[^\r\n]*", "");
            var phases = following.Split("COMMIT;", StringSplitOptions.TrimEntries);
            await using (var tx = await c.BeginTransactionAsync())
            {
                await c.ExecuteAsync(new CommandDefinition(block, transaction: tx));
                foreach (var statement in phases[0].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    await c.ExecuteAsync(new CommandDefinition(statement, transaction: tx));
                await tx.CommitAsync();
            }
            foreach (var phase in phases.Skip(1))
            {
                // Release native cached dynamic statements before dropping their source metadata.
                await c.CloseAsync(); await c.OpenAsync();
                await using (var tx = await c.BeginTransactionAsync())
                {
                    foreach (var statement in phase.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        await c.ExecuteAsync(new CommandDefinition(statement, transaction: tx));
                    await tx.CommitAsync();
                }
            }
            }
            Assert.Equal(document, await c.ExecuteScalarAsync<string>("SELECT DOCUMENTO FROM ORCAMENTOPOLITICA WHERE CODORCAMENTOPOLITICA=1"));
            await OrcamentoSchema.MigrateAsync(factory); await OrcamentoSchema.MigrateAsync(factory);
            Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME STARTING WITH 'ORCAMENTO' AND RDB$FIELD_NAME='ID'"));
            Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$RELATION_NAME STARTING WITH 'ORCAMENTO' AND RDB$CONSTRAINT_TYPE IN ('PRIMARY KEY','FOREIGN KEY','UNIQUE') AND RDB$CONSTRAINT_NAME STARTING WITH 'INTEG_'"));
            Assert.Equal(2, await c.ExecuteScalarAsync<long>("INSERT INTO ORCAMENTOPOLITICA(DOCUMENTO,CODUSUARIO,MOTIVO,DTHRCRIACAO) VALUES(@document,1,'teste',CURRENT_TIMESTAMP) RETURNING CODORCAMENTOPOLITICA", new { document }));
            await Assert.ThrowsAsync<FbException>(() => c.ExecuteAsync(block));
        }
        finally { FbConnection.DropDatabase(cs); }
    }
    private sealed class Factory(string cs) : IFirebirdConnectionFactory
    {
        public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default) { var c = new FbConnection(cs); await c.OpenAsync(ct); return c; }
    }
}
