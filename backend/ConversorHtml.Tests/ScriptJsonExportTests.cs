using System.Reflection;
using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Authorization;
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
            await connection.ExecuteAsync("CREATE TABLE SCRIPTLAUDO (CODSCRIPTLAUDO INTEGER PRIMARY KEY, ARQUIVO_JSON BLOB SUB_TYPE BINARY, NOME VARCHAR(100), SISTEMA VARCHAR(30))");
            await connection.ExecuteAsync("CREATE TABLE SCRIPTVERSOES (CODVERSAO INTEGER PRIMARY KEY, CODSCRIPTLAUDO INTEGER, ATIVO CHAR(1), DATA_CRIACAO TIMESTAMP, ARQUIVO_JSON BLOB SUB_TYPE BINARY)");
            await connection.ExecuteAsync("CREATE TABLE SCRIPTLAUDOMRD (CODSCRIPTMRD INTEGER PRIMARY KEY, CODSCRIPTLAUDO INTEGER, NOME_ARQUIVO VARCHAR(100), ARQUIVO_MRD BLOB SUB_TYPE BINARY)");
            await connection.ExecuteAsync("CREATE TABLE SCRIPTVERSAOMRD (CODVERSAOMRD INTEGER PRIMARY KEY, CODVERSAO INTEGER, NOME_ARQUIVO VARCHAR(100), ARQUIVO_MRD BLOB SUB_TYPE BINARY)");
            var principal = "{\"origem\":\"principal\"}"u8.ToArray();
            var latest = "{\"origem\":\"ultima-versao\"}"u8.ToArray();
            await connection.ExecuteAsync("INSERT INTO SCRIPTLAUDO VALUES (@id, @json, 'Modelo', 'Laudos UX')", new[] {
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
            await connection.ExecuteAsync("CREATE TABLE SCRIPTARQUIVOS (CODARQUIVO INTEGER PRIMARY KEY, TIPO VARCHAR(20), CAMINHO VARCHAR(100))");
            await connection.ExecuteAsync("CREATE TABLE SCRIPTVERSAOARQUIVOS (CODARQUIVO INTEGER PRIMARY KEY, CAMINHO VARCHAR(100))");
            await connection.ExecuteAsync("INSERT INTO SCRIPTARQUIVOS VALUES (10, 'PDF', NULL)");
            await connection.ExecuteAsync("INSERT INTO SCRIPTARQUIVOS VALUES (11, 'IMAGEM', NULL)");
            await connection.ExecuteAsync("INSERT INTO SCRIPTARQUIVOS VALUES (12, 'OUTRO', NULL)");
            await connection.ExecuteAsync("INSERT INTO SCRIPTVERSAOARQUIVOS VALUES (10, NULL)");
            Assert.IsType<OkObjectResult>(await controller.DeleteScriptImage(10));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SCRIPTARQUIVOS WHERE CODARQUIVO=10"));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SCRIPTVERSAOARQUIVOS WHERE CODARQUIVO=10"));
            Assert.IsType<OkObjectResult>(await controller.DeleteVersaoAnexo(10));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SCRIPTVERSAOARQUIVOS WHERE CODARQUIVO=10"));
            Assert.IsType<OkObjectResult>(await controller.DeleteScriptImage(11));
            Assert.IsType<BadRequestObjectResult>(await controller.DeleteScriptImage(12));
            Assert.IsType<BadRequestObjectResult>(await controller.DeleteScriptImage(999));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SCRIPTARQUIVOS WHERE CODARQUIVO=12"));

            foreach (var (id, expected) in new[] { (1, principal), (2, latest), (3, principal) })
            {
                var result = Assert.IsType<FileContentResult>(await controller.ExportJson(id));
                Assert.Equal("application/json", result.ContentType);
                Assert.Equal(expected, result.FileContents);
            }
            Assert.IsType<NotFoundResult>(await controller.ExportJson(999));
            var withoutMrd = Assert.IsType<FileContentResult>(await controller.ExportPackage(3));
            Assert.Equal("application/json", withoutMrd.ContentType);
            Assert.Equal(principal, withoutMrd.FileContents);
            var jsonOnlyBatch = Assert.IsType<FileContentResult>(await controller.ExportBatch(new([1, 3])));
            Assert.Equal(2, ReadZip(jsonOnlyBatch.FileContents).Count);

            await connection.ExecuteAsync("INSERT INTO SCRIPTLAUDOMRD VALUES (@id, @script, @name, @bytes)", new[] {
                new { id = 1, script = 1, name = "base.mrd", bytes = "base-1"u8.ToArray() },
                new { id = 2, script = 1, name = "base.mrd", bytes = "base-2"u8.ToArray() },
                new { id = 3, script = 2, name = "principal.mrd", bytes = "nao-incluir"u8.ToArray() },
                new { id = 4, script = 3, name = "../fallback.mrd", bytes = "fallback"u8.ToArray() }
            });
            await connection.ExecuteAsync("INSERT INTO SCRIPTVERSAOMRD VALUES (@id, @version, @name, @bytes)", new[] {
                new { id = 1, version = 1, name = "antigo.mrd", bytes = "nao-incluir"u8.ToArray() },
                new { id = 2, version = 2, name = "versao.mrd", bytes = "ultima-1"u8.ToArray() },
                new { id = 3, version = 2, name = "versao.mrd", bytes = "ultima-2"u8.ToArray() },
                new { id = 4, version = 3, name = "inativo.mrd", bytes = "nao-incluir"u8.ToArray() }
            });
            var individual = Assert.IsType<FileContentResult>(await controller.ExportPackage(2));
            Assert.Equal("application/zip", individual.ContentType);
            var individualFiles = ReadZip(individual.FileContents);
            Assert.Equal(3, individualFiles.Count);
            Assert.Equal(Encoding.UTF8.GetString(latest), individualFiles["Modelo_2/Modelo.json"]);
            Assert.Equal("ultima-1", individualFiles["Modelo_2/mrds/versao.json"]);
            Assert.Equal("ultima-2", individualFiles["Modelo_2/mrds/versao_3.json"]);

            var batch = Assert.IsType<FileContentResult>(await controller.ExportBatch(new([1, 2, 3, 1])));
            Assert.Equal("application/zip", batch.ContentType);
            var files = ReadZip(batch.FileContents);
            Assert.Equal(8, files.Count);
            Assert.Equal(Encoding.UTF8.GetString(principal), files["Modelo_1/Modelo.json"]);
            Assert.Equal("base-1", files["Modelo_1/mrds/base.json"]);
            Assert.Equal("base-2", files["Modelo_1/mrds/base_2.json"]);
            Assert.Equal("fallback", files["Modelo_3/mrds/.._fallback.json"]);
            foreach (var name in new[] { "RECEITA ESPECIAL", "RECEITA ESPECIAL.mrd", "RECEITA ESPECIAL.json" })
            {
                await connection.ExecuteAsync("UPDATE SCRIPTLAUDOMRD SET NOME_ARQUIVO=@name WHERE CODSCRIPTMRD=1", new { name });
                var mrd = Assert.IsType<FileContentResult>(await controller.ExportMrd(1, 1));
                Assert.Equal("RECEITA ESPECIAL.json", mrd.FileDownloadName);
                Assert.Equal("application/json", mrd.ContentType);
                Assert.Equal("base-1"u8.ToArray(), mrd.FileContents);
            }
            Assert.DoesNotContain(files.Values, content => content == "nao-incluir");
            Assert.All(files.Keys, path => Assert.DoesNotContain("../", path));

            await connection.ExecuteAsync("INSERT INTO SCRIPTLAUDO VALUES (4, @json, 'Flex', 'Laudos Flex')", new { json = principal });
            await connection.ExecuteAsync("INSERT INTO SCRIPTLAUDO VALUES (5, NULL, 'Sem JSON', 'Laudos UX')");
            foreach (var ids in new int[]?[] { null, [], [1, 999], [1, 4], [1, 5] })
                Assert.IsType<BadRequestObjectResult>(await controller.ExportBatch(new(ids)));
            Assert.IsType<NotFoundResult>(await controller.ExportPackage(999));
            Assert.IsType<BadRequestObjectResult>(await controller.ExportPackage(4));
            foreach (var method in new[] { nameof(WebScriptsController.ExportPackage), nameof(WebScriptsController.ExportBatch) })
                Assert.NotNull(typeof(WebScriptsController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>());
        }
        finally
        {
            FbConnection.DropDatabase(connectionString);
        }
    }

    private static Dictionary<string, string> ReadZip(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(entry => entry.FullName, entry => {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
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
