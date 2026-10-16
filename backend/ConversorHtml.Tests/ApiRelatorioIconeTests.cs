using System.Text.Json;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.Cadastros;

namespace ConversorHtml.Tests;

public sealed class ApiRelatorioIconeTests
{
    [FirebirdPublicationFact]
    public async Task ReportIconsResolveCodeThenLegacyNameAndServeCacheablePng()
    {
        var cs = new FbConnectionStringBuilder
        {
            Database = Path.Combine(Path.GetTempPath(), $"report-api-icons-{Guid.NewGuid():N}.fdb"),
            ServerType = FbServerType.Embedded,
            ClientLibrary = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"),
            UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false
        }.ToString();
        FbConnection.CreateDatabase(cs, overwrite: false);
        try
        {
            var factory = new Factory(cs);
            using var resource = typeof(PacoteIcone).Assembly.GetManifestResourceStream("MdwConteudos.Api.Assets.Pacotes.consulta.png")!;
            using var buffer = new MemoryStream();
            await resource.CopyToAsync(buffer);
            var bytes = buffer.ToArray();
            var icon = new PacoteIcone.TextBlob(PacoteIcone.Prefix + Convert.ToBase64String(bytes));
            await using (var c = await factory.OpenConnectionAsync())
            {
                await c.ExecuteAsync("CREATE TABLE MODULORELATORIO (CODMODULO INTEGER PRIMARY KEY, NOME VARCHAR(100), ICONE_BASE64 BLOB SUB_TYPE TEXT CHARACTER SET UTF8)");
                await c.ExecuteAsync("CREATE TABLE RELATORIOS (CODRELATORIO INTEGER PRIMARY KEY,NOME VARCHAR(100),CODMODULO INTEGER,MODULO VARCHAR(100),FORMATO VARCHAR(20),ATIVO SMALLINT,TEM_MULTISELECAO SMALLINT,DTHRCRIACAO TIMESTAMP)");
                await c.ExecuteAsync("CREATE TABLE RELATORIOVALIDACOES (CODRELATORIO INTEGER)");
                await c.ExecuteAsync("CREATE TABLE SISTEMA_MODULO (CODSISTEMA INTEGER,CODMODULO INTEGER)");
                await c.ExecuteAsync("INSERT INTO MODULORELATORIO VALUES (1,'Agenda',@icon)", new { icon });
                await c.ExecuteAsync("INSERT INTO MODULORELATORIO VALUES (2,'Financeiro',@icon)", new { icon });
                await c.ExecuteAsync("INSERT INTO MODULORELATORIO VALUES (3,'Sem icone',NULL)");
                await c.ExecuteAsync("INSERT INTO MODULORELATORIO VALUES (4,'Agenda',@icon)", new { icon });
                var reports = new[]
                {
                    (Id: 1, Code: (int?)1, Name: "Financeiro"),
                    (Id: 2, Code: (int?)null, Name: " agenda "),
                    (Id: 3, Code: (int?)null, Name: "Desconhecido"),
                    (Id: 4, Code: (int?)3, Name: "Agenda"),
                    (Id: 5, Code: (int?)999, Name: "Agenda"),
                    (Id: 6, Code: (int?)null, Name: (string?)null),
                    (Id: 7, Code: (int?)2, Name: "Financeiro"),
                    (Id: 8, Code: (int?)1, Name: "Agenda")
                };
                foreach (var r in reports)
                {
                    await c.ExecuteAsync("INSERT INTO RELATORIOS VALUES (@Id,@reportName,@Code,@moduleName,@format,@active,@multi,@date)",
                        new { r.Id, reportName = $"Relatorio ativo {r.Id}", r.Code, moduleName = r.Name, format = r.Id == 7 ? "XML" : "JSON", active = r.Id == 7 ? 0 : 1, multi = r.Id == 7 ? 1 : 0, date = new DateTime(2026, 1, r.Id) });
                    if (r.Id != 8) await c.ExecuteAsync("INSERT INTO RELATORIOVALIDACOES VALUES (@Id)", new { r.Id });
                }
                await c.ExecuteAsync("INSERT INTO RELATORIOVALIDACOES VALUES (1)");
                await c.ExecuteAsync("INSERT INTO SISTEMA_MODULO VALUES (7,1)");
                await c.ExecuteAsync("INSERT INTO SISTEMA_MODULO VALUES (7,1)");
            }
            var api = new ApiPublicaService(factory, Options.Create(new ApiPartnerOptions()), null!);
            var result = Assert.IsType<OkObjectResult>(await api.GetRelatoriosAsync(null, null, null, null, null, null, default));
            var payload = JsonSerializer.SerializeToElement(result.Value);
            Assert.Equal(7, payload.GetProperty("total").GetInt32());
            var data = payload.GetProperty("data").EnumerateArray().ToDictionary(r => r.GetProperty("codrelatorio").GetInt32());
            foreach (var id in new[] { 1, 2, 5, 7 })
                Assert.Equal(PacoteIcone.Prefix + Convert.ToBase64String(bytes), data[id].GetProperty("modulo_icone_base64").GetString());
            foreach (var id in new[] { 3, 4, 6 })
                Assert.Equal(JsonValueKind.Null, data[id].GetProperty("modulo_icone_base64").ValueKind);
            foreach (var id in new[] { 1, 2, 5 })
                Assert.Equal("/apiconteudos/v1/relatorios/modulos/1/icone", data[id].GetProperty("modulo_icone_url").GetString());
            foreach (var id in new[] { 3, 4, 6 })
                Assert.Equal(JsonValueKind.Null, data[id].GetProperty("modulo_icone_url").ValueKind);
            Assert.Equal("/apiconteudos/v1/relatorios/modulos/2/icone", data[7].GetProperty("modulo_icone_url").GetString());
            Assert.Equal(new[] { 7, 6, 5, 4, 3, 2, 1 }, payload.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("codrelatorio").GetInt32()));
            var filtered = Assert.IsType<OkObjectResult>(await api.GetRelatoriosAsync("1", "0", "ativo", "Financeiro", "json", 7, default));
            var filteredData = JsonSerializer.SerializeToElement(filtered.Value).GetProperty("data");
            Assert.Equal(1, Assert.Single(filteredData.EnumerateArray()).GetProperty("codrelatorio").GetInt32());

            var controller = new ApiPublicaController(api) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            var image = Assert.IsType<FileContentResult>(await controller.ModuloRelatorioIcone(1, default));
            Assert.Equal("private, no-cache", controller.Response.Headers.CacheControl);
            Assert.Equal("image/png", image.ContentType);
            Assert.Equal(bytes, image.FileContents);
            Assert.NotNull(image.EntityTag);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddControllers();
            await using var provider = services.BuildServiceProvider();
            var http = new DefaultHttpContext { RequestServices = provider };
            http.Request.Method = "GET";
            http.Request.Headers.IfNoneMatch = image.EntityTag.ToString();
            http.Response.Body = new MemoryStream();
            await image.ExecuteResultAsync(new ActionContext(http, new RouteData(), new ActionDescriptor()));
            Assert.Equal(304, http.Response.StatusCode);
            Assert.Equal(0, http.Response.Body.Length);
            Assert.IsType<NotFoundObjectResult>(await api.GetModuloRelatorioIconeAsync(3, default));
            Assert.IsType<NotFoundObjectResult>(await api.GetModuloRelatorioIconeAsync(999, default));
        }
        finally { FbConnection.DropDatabase(cs); }
    }

    private sealed class Factory(string cs) : IFirebirdConnectionFactory
    {
        public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default)
        {
            var c = new FbConnection(cs);
            await c.OpenAsync(ct);
            return c;
        }
    }
}
