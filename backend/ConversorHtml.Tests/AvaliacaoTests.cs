using System.Text.Json;
using System.Dynamic;
using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Avaliacoes;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.Permissions;

namespace ConversorHtml.Tests;

public sealed class AvaliacaoTests : IAsyncLifetime
{
    private string cs = "";
    private TestFactory factory = null!;
    private AvaliacaoService service = null!;
    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"))) return;
        cs = new FbConnectionStringBuilder {
            Database = Path.Combine(Path.GetTempPath(), $"avaliacao-test-{Guid.NewGuid():N}.fdb"),
            ServerType = FbServerType.Embedded, ClientLibrary = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"),
            UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false
        }.ToString();
        FbConnection.CreateDatabase(cs);
        factory = new(cs); service = new(factory);
        await using var conn = await factory.OpenConnectionAsync();
        await conn.ExecuteAsync("CREATE TABLE SCRIPTLAUDO (CODSCRIPTLAUDO INTEGER PRIMARY KEY, NOME VARCHAR(252), SISTEMA VARCHAR(30), CODPACOTE INTEGER)");
        await conn.ExecuteAsync("CREATE TABLE SCRIPTVERSOES (CODVERSAO INTEGER PRIMARY KEY, CODSCRIPTLAUDO INTEGER, NUMERO_VERSAO VARCHAR(100))");
        await conn.ExecuteAsync("INSERT INTO SCRIPTLAUDO VALUES (1,'Ecocardiograma','Laudos UX',10)");
        await conn.ExecuteAsync("INSERT INTO SCRIPTLAUDO VALUES (2,'Risco','Laudos Flex',20)");
        await conn.ExecuteAsync("INSERT INTO SCRIPTVERSOES VALUES (11,1,'V1')");
        await conn.ExecuteAsync("INSERT INTO SCRIPTVERSOES VALUES (12,1,'V2')");
        await conn.ExecuteAsync("INSERT INTO SCRIPTVERSOES VALUES (21,2,'V1')");
        await conn.ExecuteAsync("ALTER TABLE SCRIPTVERSOES ADD ATIVO CHAR(1) DEFAULT 'T'");
        await conn.ExecuteAsync("ALTER TABLE SCRIPTVERSOES ADD DATA_CRIACAO TIMESTAMP");
        await conn.ExecuteAsync("UPDATE SCRIPTVERSOES SET ATIVO='T',DATA_CRIACAO=DATE '2026-01-01'");
        await conn.ExecuteAsync("UPDATE SCRIPTVERSOES SET DATA_CRIACAO=DATE '2026-02-01' WHERE CODVERSAO=12");
        await AvaliacaoSchema.MigrateAsync(factory);
    }
    public Task DisposeAsync() { if (!string.IsNullOrEmpty(cs)) FbConnection.DropDatabase(cs); return Task.CompletedTask; }
    private static AvaliacaoInput Input(int? version = 11, string client = "clinica-1", string user = "usuario-1", int rating = 4) => new() {
        CodVersao = version, Origem = "portal", ClienteId = client, UsuarioId = user,
        NomeAvaliador = "João", NomeClinica = "Clínica Coração", NotaGeral = rating, Recomendacao = "sim", Comentario = "Ótima avaliação, ação útil.", FacilidadeUso = 5
    };

    [FirebirdPublicationFact]
    public async Task UpsertPreservesHistorySnapshotsAndModeration()
    {
        await AvaliacaoSchema.MigrateAsync(factory);
        var created = await service.SaveAsync(1, Input());
        Assert.True(created.Criada); Assert.False(created.Atualizada);
        var repeated = await service.SaveAsync(1, Input());
        Assert.Equal(created.Id, repeated.Id); Assert.False(repeated.Criada); Assert.False(repeated.Atualizada);
        Assert.Single((await service.DetailAsync(created.Id, default)).Historico);
        await service.ChangeAsync(created.Id, "triagem", "concluida", null, "administrador", 1, default);
        await service.ChangeAsync(created.Id, "anotacao", "Análise interna privada", null, "administrador", 1, default);
        await service.ChangeAsync(created.Id, "visibilidade", "Verificar comentário", false, "administrador", 1, default);
        var before = await service.SummaryAsync(new(), true, default);
        Assert.Equal(0, before.Quantidade); Assert.Null(before.MediaGeral);
        Assert.Empty((await service.ListAsync(new(), true, default)).Data);
        var own = await service.MineAsync(1, 11, "portal", "clinica-1", "usuario-1", default);
        Assert.NotNull(own); Assert.Equal("João", own.NomeAvaliador);
        await using (var conn = await factory.OpenConnectionAsync())
            await conn.ExecuteAsync("UPDATE SCRIPTLAUDO SET NOME='Novo nome' WHERE CODSCRIPTLAUDO=1");
        var edit = Input(rating: 2);
        edit.Comentario = "Revisão: precisa melhorar.";
        var updated = await service.SaveAsync(1, edit);
        Assert.Equal(created.Id, updated.Id); Assert.True(updated.Atualizada);
        var detail = await service.DetailAsync(created.Id, default);
        Assert.False(detail.Avaliacao.Visivel); Assert.Equal("nova", detail.Avaliacao.Situacao);
        Assert.Equal("Ecocardiograma", detail.Avaliacao.NomeScript);
        Assert.Equal(5, detail.Historico.Count);
        Assert.Contains("NOTA", detail.Historico.First().Antes!.ToUpperInvariant());
        Assert.Contains("Ótima", JsonSerializer.Deserialize<AvaliacaoRecord>(detail.Historico.First().Antes!)!.Comentario);
        await service.ChangeAsync(created.Id, "visibilidade", "Revisada", true, "administrador", 1, default);
        var summary = await service.SummaryAsync(new(), true, default);
        Assert.Equal(1, summary.Quantidade); Assert.Equal(2d, summary.MediaGeral); Assert.Equal(5d, summary.FacilidadeUso); Assert.Null(summary.Completude);
        Assert.Equal(1, summary.DistribuicaoNotas[2]); Assert.Equal(1, summary.Recomendacoes["sim"]);
        var json = JsonSerializer.Serialize((await service.ListAsync(new(), true, default)).Data.Select(x => x.Publica()));
        Assert.DoesNotContain("ClienteId", json); Assert.DoesNotContain("NomeClinica", json); Assert.DoesNotContain("Origem", json);
        Assert.DoesNotContain("privada", json); Assert.DoesNotContain("Visivel", json); Assert.DoesNotContain("Situacao", json);
    }

    [FirebirdPublicationFact]
    public async Task IdentityDistinguishesClinicsUsersVersionsAndPrincipal()
    {
        var requests = new[] { Input(), Input(client: "clinica-2"), Input(user: "usuario-2"), Input(12), Input(null) };
        var ids = new List<long>();
        foreach (var input in requests) ids.Add((await service.SaveAsync(1, input)).Id);
        Assert.Equal(5, ids.Distinct().Count());
        Assert.Equal(3, (await service.SummaryAsync(new() { Versao = 11 }, true, default)).Quantidade);
        Assert.Equal(1, (await service.SummaryAsync(new() { Versao = 0 }, true, default)).Quantidade);
        Assert.Equal(2, (await service.ListAsync(new() { PageSize = 2 }, false, default)).Data.Count);
        Assert.Equal(3, (await service.ListAsync(new() { PageSize = 2 }, false, default)).TotalPages);
        Assert.Equal(1, (await service.ListAsync(new() { Script = 1, Versao = 12, Sistema = "Laudos UX", Pacote = 10, Origem = "portal", Clinica = "Coração", Nota = 4, Recomendacao = "sim", Situacao = "nova", Visivel = true, Inicio = DateTime.UtcNow.Date, Fim = DateTime.UtcNow.Date }, false, default)).TotalItems);
        Assert.Equal(0, (await service.ListAsync(new() { Pacote = 20 }, false, default)).TotalItems);
    }

    [FirebirdPublicationFact]
    public async Task ConcurrentFirstSubmissionsHaveOneRowAndOneEvent()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => service.SaveAsync(1, Input())));
        Assert.Single(results.Select(x => x.Id).Distinct());
        Assert.Single(results, x => x.Criada);
        Assert.Single((await service.DetailAsync(results[0].Id, default)).Historico);
    }

    [FirebirdPublicationFact]
    public async Task InvalidRequestsAreRejectedWithoutPersisting()
    {
        foreach (var input in new[] { Input(21), Input(0), Input(-1), Input(rating: 0), Input(rating: 6), Input(client: " ") })
            Assert.Equal(400, (await Assert.ThrowsAsync<AvaliacaoException>(() => service.SaveAsync(1, input))).Status);
        var invalid = Input(); invalid.Comentario = new string('a', 1001);
        await Assert.ThrowsAsync<AvaliacaoException>(() => service.SaveAsync(1, invalid));
        invalid = Input(); invalid.FacilidadeUso = 0;
        await Assert.ThrowsAsync<AvaliacaoException>(() => service.SaveAsync(1, invalid));
        invalid = Input(); invalid.Recomendacao = "outro";
        await Assert.ThrowsAsync<AvaliacaoException>(() => service.SaveAsync(1, invalid));
        Assert.Equal(404, (await Assert.ThrowsAsync<AvaliacaoException>(() => service.SaveAsync(99, Input(null)))).Status);
        Assert.Equal(0, (await service.SummaryAsync(new(), false, default)).Quantidade);
        var id = (await service.SaveAsync(1, Input())).Id;
        await Assert.ThrowsAsync<AvaliacaoException>(() => service.ChangeAsync(id, "visibilidade", " ", false, "admin", 1, default));
        Assert.True((await service.DetailAsync(id, default)).Avaliacao.Visivel);
        await using var conn = await factory.OpenConnectionAsync();
        await conn.ExecuteAsync("DROP TABLE SCRIPT_AVALIACAO_EVENTO");
        Assert.Equal(503, (await Assert.ThrowsAsync<AvaliacaoException>(() => service.ListAsync(new(), false, default))).Status);
    }

    [Fact]
    public void VersionIsExplicitAndInternalRoutesUseEvaluationPermissions()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AvaliacaoInput>("{}"));
        Assert.Equal(PermissionDomains.Avaliacoes, ApiPartnerJwtMiddleware.InternalDomain("/api/v1/scripts/1/avaliacoes/resumo"));
        Assert.Equal(PermissionDomains.Scripts, ApiPartnerJwtMiddleware.InternalDomain("/api/v1/scripts/1/download"));
    }

    [FirebirdPublicationFact]
    public async Task ThinScriptListingCarriesTheEvaluatedVersion()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DataPaths:Root"] = Path.GetTempPath() }).Build();
        var scripts = new ApiScriptOperations(factory, config, new TestEnvironment());
        var map = typeof(ApiScriptOperations).GetMethod("MapScriptRowAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await using var conn = await factory.OpenConnectionAsync();
        foreach (var id in new[] { 1, 99 })
        {
            IDictionary<string, object?> row = new ExpandoObject();
            foreach (var name in new[] { "NOME","DESCRICAO","LINGUAGEM","SISTEMA","APROVADO_POR","NOME_PACOTE" }) row[name] = "teste";
            row["CODSCRIPTLAUDO"] = id; row["APROVADO"] = 1; row["ATIVO"] = 1;
            row["DATA_VERIFICACAO"] = null; row["CODPACOTE"] = 10; row["PACOTE_ICONE_BASE64"] = null;
            var result = await (Task<Dictionary<string, object?>>)map.Invoke(scripts, [conn, row, false, CancellationToken.None])!;
            Assert.True(result.ContainsKey("codversao"));
            Assert.Equal(id == 1 ? (object)12 : null, result["codversao"]);
            Assert.False(result.ContainsKey("imagens"));
        }
    }

    [FirebirdPublicationFact]
    public async Task HttpIntegrationValidatesBodiesAndProtectsPublicProjection()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(AvaliacaoPublicaController).Assembly);
        builder.Services.AddSingleton(service);
        const string secret = "synthetic-http-evaluation-secret-2026";
        builder.Services.Configure<ApiPartnerOptions>(opts => { opts.JwtSecret = secret; opts.JwtPassword = "test"; });
        await using var app = builder.Build();
        app.UseMiddleware<ApiPartnerJwtMiddleware>();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            const string path = "/apiconteudos/v1/scripts/1/avaliacoes";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
            var token = ApiJwtKeyHelper.CreateToken(new { senha = "test", datahora = DateTime.UtcNow.ToString("O") }, secret);
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new { nota_geral = 4 })).StatusCode);
            var response = await client.PutAsJsonAsync(path, Input());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            var id = created.GetProperty("data").GetProperty("id").GetInt64();
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, Input())).StatusCode);
            var publicList = await client.GetStringAsync(path);
            Assert.Contains("nomeAvaliador", publicList); Assert.DoesNotContain("clienteId", publicList); Assert.DoesNotContain("nomeClinica", publicList);
            Assert.DoesNotContain("situacao", publicList); Assert.DoesNotContain("historico", publicList);
            await service.ChangeAsync(id, "visibilidade", "Privada", false, "administrador", 1, default);
            var hiddenList = await client.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal(0, hiddenList.GetProperty("totalItems").GetInt32());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "/minha?codversao=11&origem=portal&cliente_id=clinica-1&usuario_id=usuario-1")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "/minha?origem=portal&cliente_id=clinica-1&usuario_id=usuario-1")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?pageSize=101")).StatusCode);
        }
        finally { await app.StopAsync(); }
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

    private sealed class TestFactory(string connectionString) : IFirebirdConnectionFactory
    {
        public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default) {
            var conn = new FbConnection(connectionString); await conn.OpenAsync(ct); return conn;
        }
    }
}
