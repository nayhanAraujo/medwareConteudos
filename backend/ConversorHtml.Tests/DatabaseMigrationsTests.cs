using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;

namespace ConversorHtml.Tests;

public sealed class DatabaseMigrationsTests
{
    [FirebirdPublicationFact]
    public async Task DeploymentUpgradesStaleCatalogAndCanRepeatWithoutChangingGrantsOrIcons()
    {
        var cs = new FbConnectionStringBuilder {
            Database = Path.Combine(Path.GetTempPath(), $"deploy-migrations-{Guid.NewGuid():N}.fdb"),
            ServerType = FbServerType.Embedded,
            ClientLibrary = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"),
            UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false
        }.ToString();
        FbConnection.CreateDatabase(cs, overwrite: false);
        try
        {
            var factory = new Factory(cs);
            var permissions = new PermissionService(factory);
            await using var services = new ServiceCollection()
                .AddSingleton<IFirebirdConnectionFactory>(factory)
                .AddSingleton<IPermissionService>(permissions).BuildServiceProvider();
            await using (var conn = await factory.OpenConnectionAsync())
            {
                await conn.ExecuteAsync("CREATE TABLE PACOTES (CODPACOTE INTEGER PRIMARY KEY, NOME VARCHAR(100))");
                await conn.ExecuteAsync("INSERT INTO PACOTES VALUES (1,'Cardiologia')");
                await conn.ExecuteAsync("CREATE TABLE MODULORELATORIO (CODMODULO INTEGER PRIMARY KEY)");
                await conn.ExecuteAsync("CREATE TABLE SCRIPTLAUDO (CODSCRIPTLAUDO INTEGER PRIMARY KEY)");
                await conn.ExecuteAsync("CREATE TABLE SCRIPTVERSOES (CODVERSAO INTEGER PRIMARY KEY)");
            }
            await permissions.EnsureSchemaAsync();
            await using (var conn = await factory.OpenConnectionAsync())
            {
                await conn.ExecuteAsync("DELETE FROM PERMISSAO WHERE DOMINIO='avaliacoes'");
                await conn.ExecuteAsync("INSERT INTO PERFIL_PERMISSAO (PERFIL,CHAVE) VALUES ('usuario','scripts.editar')");
                await conn.ExecuteAsync("INSERT INTO USUARIO_PERMISSAO (CODUSUARIO,CHAVE,MODO) VALUES (42,'scripts.editar','NEGAR')");
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => permissions.ValidateSchemaAsync());

            // A failure after the catalog reset must not disable the previous release's permissions.
            await using (var conn = await factory.OpenConnectionAsync())
            {
                await conn.ExecuteAsync("CREATE EXCEPTION MIGRATION_TEST_FAILURE 'Forced seed failure'");
                await conn.ExecuteAsync("CREATE TRIGGER FAIL_CATALOG_SEED FOR PERMISSAO ACTIVE BEFORE INSERT OR UPDATE AS BEGIN IF (NEW.CHAVE='avaliacoes.visualizar' AND NEW.STATUS=-1) THEN EXCEPTION MIGRATION_TEST_FAILURE; END");
            }
            await Assert.ThrowsAsync<FbException>(() => DatabaseMigrations.RunAsync(services, NullLogger.Instance));
            await using (var conn = await factory.OpenConnectionAsync())
            {
                Assert.Equal(-1, await conn.ExecuteScalarAsync<int>("SELECT STATUS FROM PERMISSAO WHERE CHAVE='scripts.editar'"));
                Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='SCRIPT_AVALIACAO'"));
                await conn.ExecuteAsync("DROP TRIGGER FAIL_CATALOG_SEED");
                await conn.ExecuteAsync("DROP EXCEPTION MIGRATION_TEST_FAILURE");
            }

            await DatabaseMigrations.RunAsync(services, NullLogger.Instance);
            await permissions.ValidateSchemaAsync();
            await using (var conn = await factory.OpenConnectionAsync())
            {
                Assert.True(await conn.ExecuteScalarAsync<int>("SELECT OCTET_LENGTH(ICONE_BASE64) FROM PACOTES WHERE CODPACOTE=1") > 0);
                await conn.ExecuteAsync("UPDATE PACOTES SET ICONE_BASE64='custom-icon' WHERE CODPACOTE=1");
            }

            await DatabaseMigrations.RunAsync(services, NullLogger.Instance);
            await using (var conn = await factory.OpenConnectionAsync())
            {
                Assert.Equal(2, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME IN ('SCRIPT_AVALIACAO','SCRIPT_AVALIACAO_EVENTO')"));
                Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='MODULORELATORIO' AND RDB$FIELD_NAME='ICONE_BASE64'"));
                Assert.Equal("custom-icon", await conn.ExecuteScalarAsync<string>("SELECT ICONE_BASE64 FROM PACOTES WHERE CODPACOTE=1"));
                Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM PERFIL_PERMISSAO WHERE PERFIL='usuario' AND CHAVE='scripts.editar'"));
                Assert.Equal("NEGAR", await conn.ExecuteScalarAsync<string>("SELECT MODO FROM USUARIO_PERMISSAO WHERE CODUSUARIO=42"));
            }
        }
        finally { FbConnection.DropDatabase(cs); }
    }

    private sealed class Factory(string cs) : IFirebirdConnectionFactory
    {
        public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default)
        {
            var conn = new FbConnection(cs);
            await conn.OpenAsync(ct);
            return conn;
        }
    }
}
