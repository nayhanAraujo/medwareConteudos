using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Web;

namespace ConversorHtml.Tests;

public sealed class ScriptCodigoUniversalTests
{
    [FirebirdPublicationFact]
    public async Task MigrationAndCodesAreRepeatableUniqueAndImmutable()
    {
        var cs = new FbConnectionStringBuilder {
            Database = Path.Combine(Path.GetTempPath(), $"script-code-{Guid.NewGuid():N}.fdb"),
            ServerType = FbServerType.Embedded, ClientLibrary = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"),
            UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false
        }.ToString();
        FbConnection.CreateDatabase(cs);
        try
        {
            var factory = new Factory(cs);
            await using var conn = await factory.OpenConnectionAsync();
            await conn.ExecuteAsync("CREATE TABLE SCRIPTLAUDO (CODSCRIPTLAUDO INTEGER NOT NULL CONSTRAINT PK_SCRIPTLAUDO PRIMARY KEY, NOME VARCHAR(100) CHARACTER SET UTF8)");
            await conn.ExecuteAsync("INSERT INTO SCRIPTLAUDO VALUES (2,'Segundo')");
            await conn.ExecuteAsync("INSERT INTO SCRIPTLAUDO VALUES (1,'Primeiro')");
            await ScriptCodigoUniversalSchema.MigrateAsync(factory);
            Assert.Equal("MOD-000001", await conn.ExecuteScalarAsync<string>("SELECT CODUNIVERSAL FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=1"));
            await ScriptCodigoUniversalSchema.MigrateAsync(factory);
            Assert.Equal("MOD-000002", await conn.ExecuteScalarAsync<string>("SELECT CODUNIVERSAL FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=2"));
            await conn.ExecuteAsync("UPDATE SCRIPTLAUDO SET NOME='Novo nome' WHERE CODSCRIPTLAUDO=1");
            Assert.Equal("MOD-000001", await conn.ExecuteScalarAsync<string>("SELECT CODUNIVERSAL FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=1"));
            await Assert.ThrowsAsync<FbException>(() => conn.ExecuteAsync("UPDATE SCRIPTLAUDO SET CODUNIVERSAL='MOD-123456' WHERE CODSCRIPTLAUDO=1"));
            await Assert.ThrowsAsync<FbException>(() => conn.ExecuteAsync("UPDATE SCRIPTLAUDO SET CODUNIVERSAL=NULL WHERE CODSCRIPTLAUDO=1"));
            await conn.ExecuteAsync("DELETE FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=2");
            await conn.ExecuteAsync("INSERT INTO SCRIPTLAUDO (CODSCRIPTLAUDO,NOME,CODUNIVERSAL) VALUES (3,'Terceiro','MOD-000001')");
            Assert.Equal("MOD-000003", await conn.ExecuteScalarAsync<string>("SELECT CODUNIVERSAL FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=3"));
            var codes = await Task.WhenAll(Enumerable.Range(4, 10).Select(async id => {
                await using var other = await factory.OpenConnectionAsync();
                return await other.ExecuteScalarAsync<string>("INSERT INTO SCRIPTLAUDO (CODSCRIPTLAUDO,NOME) VALUES (@id,'Concorrente') RETURNING CODUNIVERSAL", new { id });
            }));
            Assert.Equal(10, codes.Distinct().Count());
            await conn.ExecuteAsync("ALTER SEQUENCE GEN_SCRIPTLAUDO_UNIVERSAL RESTART WITH 1000000");
            Assert.Equal("MOD-1000000", await conn.ExecuteScalarAsync<string>("INSERT INTO SCRIPTLAUDO (CODSCRIPTLAUDO) VALUES (20) RETURNING CODUNIVERSAL"));
            Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$CONSTRAINT_NAME='UNQ_SCRIPTLAUDO_CODUNIVERSAL'"));
            Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT RDB$NULL_FLAG FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='SCRIPTLAUDO' AND RDB$FIELD_NAME='CODUNIVERSAL'"));

            // Exercise the real web search and detail mapping against the migrated schema.
            foreach (var column in new[] { "DESCRICAO VARCHAR(100)", "LINGUAGEM VARCHAR(20)", "CAMINHO_PROJETO VARCHAR(100)", "SISTEMA VARCHAR(30)", "APROVADO INTEGER", "DATA_VERIFICACAO TIMESTAMP", "ATIVO INTEGER", "AMBIENTE_APROVACAO VARCHAR(250)", "ARQUIVO_JSON BLOB SUB_TYPE BINARY", "APROVADO_POR VARCHAR(100)", "CAMINHO_AZURE VARCHAR(100)", "DLL BLOB SUB_TYPE BINARY", "CRIADO_POR VARCHAR(100)", "LINK_TESTE VARCHAR(100)", "CODPACOTE INTEGER" })
                await conn.ExecuteAsync($"ALTER TABLE SCRIPTLAUDO ADD {column}");
            await conn.ExecuteAsync("CREATE TABLE PACOTES (CODPACOTE INTEGER NOT NULL CONSTRAINT PK_PACOTES PRIMARY KEY,NOME VARCHAR(100))");
            await conn.ExecuteAsync("CREATE TABLE SCRIPTLAUDOMRD (CODSCRIPTMRD INTEGER NOT NULL CONSTRAINT PK_SCRIPTLAUDOMRD PRIMARY KEY,CODSCRIPTLAUDO INTEGER,NOME_ARQUIVO VARCHAR(100),PADRAO CHAR(1),ORDEM INTEGER)");
            await conn.ExecuteAsync("CREATE TABLE SCRIPTARQUIVOS (CODARQUIVO INTEGER NOT NULL CONSTRAINT PK_SCRIPTARQUIVOS PRIMARY KEY,CODSCRIPTLAUDO INTEGER,TIPO VARCHAR(20),CAMINHO VARCHAR(100),NOME_ARQUIVO VARCHAR(100))");
            await conn.ExecuteAsync("CREATE TABLE VARIAVEIS (CODVARIAVEL INTEGER NOT NULL CONSTRAINT PK_VARIAVEIS PRIMARY KEY,VARIAVEL VARCHAR(100),NOME VARCHAR(100))");
            await conn.ExecuteAsync("CREATE TABLE SCRIPTLAUDO_VARIAVEL (CODSCRIPTLAUDO INTEGER NOT NULL,CODVARIAVEL INTEGER NOT NULL,CONSTRAINT PK_SCRIPTLAUDO_VARIAVEL PRIMARY KEY(CODSCRIPTLAUDO,CODVARIAVEL))");
            await conn.ExecuteAsync("CREATE TABLE SCRIPTVERSOES (CODVERSAO INTEGER NOT NULL CONSTRAINT PK_SCRIPTVERSOES PRIMARY KEY,CODSCRIPTLAUDO INTEGER,NUMERO_VERSAO VARCHAR(20),ARQUIVO_JSON BLOB SUB_TYPE BINARY,ATIVO CHAR(1),DATA_CRIACAO TIMESTAMP)");
            await conn.ExecuteAsync("UPDATE SCRIPTLAUDO SET SISTEMA='Laudos UX',ATIVO=1");
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DataPaths:Root"] = Path.GetTempPath() }).Build();
            var service = new ScriptsService(config, NullLogger<ScriptsService>.Instance, new TestEnvironment(), null!);
            typeof(ScriptsService).GetField("_connectionString", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, cs);
            var search = await service.ListScriptsAsync("mod-000001", "Laudos UX", null, null, 1, 1, 10);
            Assert.Equal("MOD-000001", Assert.Single(search.Items).CodUniversal);
            Assert.Equal("MOD-000001", (await service.GetScriptAsync(1))!.CodUniversal);
            Assert.Empty((await service.ListScriptsAsync("MOD-999999", null, null, null, null, 1, 10)).Items);
            const string botModelName = "Avaliação de Carótidas e Vertebrais Studio";
            await conn.ExecuteAsync("UPDATE SCRIPTLAUDO SET NOME=@name WHERE CODSCRIPTLAUDO=1", new { name = botModelName });
            Assert.Equal(1, Assert.Single((await service.ListScriptsAsync(botModelName, null, null, null, null, 1, 50)).Items).CodScriptLaudo);
            Assert.Equal(1, Assert.Single((await service.ListScriptsAsync("Carótidas", null, null, null, null, 1, 50)).Items).CodScriptLaudo);
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

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "MdwConteudos.Api";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
