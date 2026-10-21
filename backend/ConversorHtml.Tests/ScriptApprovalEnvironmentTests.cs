using System.Reflection;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using MdwConteudos.Api.Modules.Web;

namespace ConversorHtml.Tests;

public sealed class ScriptApprovalEnvironmentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnvironmentIsRequired(string? value) => Assert.Throws<InvalidOperationException>(() => ScriptsService.ValidateAmbienteAprovacao(value));

    [Fact]
    public void EnvironmentIsTrimmedAndLimited()
    {
        Assert.Equal("Homologação — Clínica", ScriptsService.ValidateAmbienteAprovacao("  Homologação — Clínica  "));
        Assert.Equal(250, ScriptsService.ValidateAmbienteAprovacao(new string('a', 250)).Length);
        Assert.Throws<InvalidOperationException>(() => ScriptsService.ValidateAmbienteAprovacao(new string('a', 251)));
    }

    [FirebirdPublicationFact]
    public async Task ApprovalsPersistEnvironmentAndClearItOnRevocation()
    {
        var cs = new FbConnectionStringBuilder {
            Database = Path.Combine(Path.GetTempPath(), $"script-approval-{Guid.NewGuid():N}.fdb"),
            ServerType = FbServerType.Embedded, ClientLibrary = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"),
            UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false
        }.ToString();
        FbConnection.CreateDatabase(cs);
        try
        {
            await using var conn = new FbConnection(cs);
            await conn.OpenAsync();
            await conn.ExecuteAsync("CREATE TABLE SCRIPTLAUDO (CODSCRIPTLAUDO INTEGER PRIMARY KEY, APROVADO INTEGER, APROVADO_POR VARCHAR(100), AMBIENTE_APROVACAO VARCHAR(250), DATA_VERIFICACAO TIMESTAMP)");
            await conn.ExecuteAsync("CREATE TABLE SCRIPTVERSOES (CODVERSAO INTEGER PRIMARY KEY, NUMERO_VERSAO VARCHAR(20), APROVADO CHAR(1), APROVADO_POR VARCHAR(100), AMBIENTE_APROVACAO VARCHAR(250), DATA_APROVACAO TIMESTAMP)");
            await conn.ExecuteAsync("CREATE TABLE SCRIPTVERSAOHISTORICO (CODHISTORICO INTEGER PRIMARY KEY, CODVERSAO_DESTINO INTEGER, TIPO_ALTERACAO VARCHAR(30), DESCRICAO VARCHAR(1000), USUARIO VARCHAR(100), DATA_ALTERACAO TIMESTAMP)");
            await conn.ExecuteAsync("CREATE TABLE SCRIPTAPROVACAOTOKEN (TOKEN VARCHAR(100) PRIMARY KEY, CODSCRIPTLAUDO INTEGER, EXPIRA_EM TIMESTAMP, USADO INTEGER, APROVADO_EM TIMESTAMP, REJEITADO_EM TIMESTAMP)");
            await conn.ExecuteAsync("INSERT INTO SCRIPTLAUDO (CODSCRIPTLAUDO,APROVADO) VALUES (1,0)");
            await conn.ExecuteAsync("INSERT INTO SCRIPTVERSOES (CODVERSAO,NUMERO_VERSAO,APROVADO) VALUES (21,'V1','F')");
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DataPaths:Root"] = Path.GetTempPath() }).Build();
            var service = new ScriptsService(config, NullLogger<ScriptsService>.Instance, new TestEnvironment(), null!);
            typeof(ScriptsService).GetField("_connectionString", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, cs);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToggleAprovacaoAsync(1, "Teste"));
            Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT APROVADO FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=1"));
            await service.ToggleAprovacaoAsync(1, "João", "  Homologação — Clínica  ");
            Assert.Equal("Homologação — Clínica", await conn.ExecuteScalarAsync<string>("SELECT AMBIENTE_APROVACAO FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=1"));
            Assert.Equal("João", await conn.ExecuteScalarAsync<string>("SELECT APROVADO_POR FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=1"));
            await service.ToggleAprovacaoAsync(1, "João");
            Assert.Null(await conn.ExecuteScalarAsync<string?>("SELECT AMBIENTE_APROVACAO FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=1"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToggleAprovacaoAsync(1, "Teste"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveVersaoAsync(21, "Teste"));
            await service.ApproveVersaoAsync(21, "João", "Laudos UX 39 — Testes");
            Assert.Equal("Laudos UX 39 — Testes", await conn.ExecuteScalarAsync<string>("SELECT AMBIENTE_APROVACAO FROM SCRIPTVERSOES WHERE CODVERSAO=21"));
            Assert.Contains("Laudos UX 39 — Testes", await conn.ExecuteScalarAsync<string>("SELECT DESCRICAO FROM SCRIPTVERSAOHISTORICO"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveVersaoAsync(21, "Outro", "Produção"));
            Assert.Equal("Laudos UX 39 — Testes", await conn.ExecuteScalarAsync<string>("SELECT AMBIENTE_APROVACAO FROM SCRIPTVERSOES WHERE CODVERSAO=21"));
            await conn.ExecuteAsync("INSERT INTO SCRIPTAPROVACAOTOKEN (TOKEN,CODSCRIPTLAUDO,EXPIRA_EM,USADO) VALUES ('teste',1,DATEADD(1 DAY TO CURRENT_TIMESTAMP),0)");
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveByTokenAsync("teste", true));
            await service.ApproveByTokenAsync("teste", true, "Teste via e-mail");
            Assert.Equal("Teste via e-mail", await conn.ExecuteScalarAsync<string>("SELECT AMBIENTE_APROVACAO FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=1"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveByTokenAsync("teste", true, "Outro ambiente"));
            await conn.ExecuteAsync("INSERT INTO SCRIPTAPROVACAOTOKEN (TOKEN,CODSCRIPTLAUDO,EXPIRA_EM,USADO) VALUES ('rejeitar',1,DATEADD(1 DAY TO CURRENT_TIMESTAMP),0)");
            await service.ApproveByTokenAsync("rejeitar", false);
            Assert.Equal("Teste via e-mail", await conn.ExecuteScalarAsync<string>("SELECT AMBIENTE_APROVACAO FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=1"));
        }
        finally { FbConnection.DropDatabase(cs); }
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
