using System.Reflection;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using MdwConteudos.Api.Modules.Web;

namespace ConversorHtml.Tests;

public sealed class ScriptJsonExportTests
{
    [FirebirdPublicationFact]
    public async Task ExportsBaseOrLatestActiveVersionAsJson()
    {
        var connectionString = new FbConnectionStringBuilder
        {
            Database = Path.Combine(Path.GetTempPath(), $"script-json-test-{Guid.NewGuid():N}.fdb"),
            ServerType = FbServerType.Embedded,
            ClientLibrary = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"),
            UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false
        }.ToString();
        FbConnection.CreateDatabase(connectionString, overwrite: false);
        try
        {
            await using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync("CREATE TABLE SCRIPTLAUDO (CODSCRIPTLAUDO INTEGER PRIMARY KEY, ARQUIVO_JSON BLOB SUB_TYPE BINARY)");
            await connection.ExecuteAsync("CREATE TABLE SCRIPTVERSOES (CODVERSAO INTEGER PRIMARY KEY, CODSCRIPTLAUDO INTEGER, ATIVO CHAR(1), DATA_CRIACAO TIMESTAMP, ARQUIVO_JSON BLOB SUB_TYPE BINARY)");
            var principal = "{\"origem\":\"principal\"}"u8.ToArray();
            var latest = "{\"origem\":\"ultima-versao\"}"u8.ToArray();
            await connection.ExecuteAsync("INSERT INTO SCRIPTLAUDO VALUES (@id, @json)", new[] {
                new { id = 1, json = principal }, new { id = 2, json = principal }, new { id = 3, json = principal }
            });
            await connection.ExecuteAsync("INSERT INTO SCRIPTVERSOES VALUES (@id, @script, @ativo, @data, @json)", new[] {
                new { id = 1, script = 2, ativo = "T", data = new DateTime(2026, 1, 1), json = principal },
                new { id = 2, script = 2, ativo = "T", data = new DateTime(2026, 2, 1), json = latest },
                new { id = 3, script = 2, ativo = "F", data = new DateTime(2026, 3, 1), json = principal }
            });
            await connection.ExecuteAsync("INSERT INTO SCRIPTVERSOES VALUES (4, 3, 'T', CURRENT_TIMESTAMP, NULL)");

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Firebird:Database"] = new FbConnectionStringBuilder(connectionString).Database,
                ["DataPaths:Root"] = Path.GetTempPath()
            }).Build();
            var service = new ScriptsService(config, NullLogger<ScriptsService>.Instance, new TestEnvironment(), null!);
            // The service builds a TCP connection; this fixture uses an isolated embedded database.
            typeof(ScriptsService).GetField("_connectionString", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(service, connectionString);
            var controller = new WebScriptsController(service);

            foreach (var (id, expected) in new[] { (1, principal), (2, latest), (3, principal) })
            {
                var result = Assert.IsType<FileContentResult>(await controller.ExportJson(id));
                Assert.Equal("application/json", result.ContentType);
                Assert.Equal(expected, result.FileContents);
            }
            Assert.IsType<NotFoundResult>(await controller.ExportJson(999));
        }
        finally
        {
            FbConnection.DropDatabase(connectionString);
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "MdwConteudos.Api";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
