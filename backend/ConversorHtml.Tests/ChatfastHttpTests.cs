using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Auditoria;
using MdwConteudos.Api.Modules.Automacoes;

namespace ConversorHtml.Tests;

public sealed partial class ChatfastFirebirdTests
{
    async Task<(WebApplication App, HttpClient Client)> Host(bool production = false, string? demoDirectory = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = production ? "Production" : "Development" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IFirebirdConnectionFactory>(factory);
        builder.Services.Configure<ChatfastOptions>(o => { o.Enabled = true; o.ResponsesKey = new('a', 64); o.ReportsKey = new('b', 64); o.DemoDirectory = demoDirectory ?? ""; });
        builder.Services.AddScoped<ChatfastService>(); builder.Services.AddScoped<ChatfastErrors>(); builder.Services.AddScoped<ActiveAuditAdminFilter>();
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, Auth>("test", _ => { }); builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(o => { o.AddPolicy("chatfast", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test")); o.AddPolicy("chatfast-public", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test")); });
        builder.Services.AddControllers().AddApplicationPart(typeof(ChatfastPublicController).Assembly);
        var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return (app, new HttpClient { BaseAddress = new Uri(url) });
    }
    [FirebirdPublicationFact] public async Task HttpEnforcesProducerScopeAndPublicProjection()
    {
        var (app, client) = await Host(); await using var dispose = app; using var http = client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/integracoes/chatfast/v1/respostas", ChatfastTests.Event())).StatusCode);
        http.DefaultRequestHeaders.Add("X-Chatfast-Key", new string('b', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/integracoes/chatfast/v1/respostas", ChatfastTests.Event())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/integracoes/chatfast/v1/relatorios", ChatfastTests.Report())).StatusCode);
        http.DefaultRequestHeaders.Remove("X-Chatfast-Key");
        var publicJson = await http.GetStringAsync("/api/publico/chatfast/v1/relatorios/diario"); Assert.DoesNotContain("CLIENTE PRIVADO", publicJson); Assert.DoesNotContain("details", publicJson); Assert.DoesNotContain("sessionId", publicJson);
        Assert.Contains("\"waiting\":null", publicJson);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/publico/chatfast/v1/relatorios/diario?date=invalid")).StatusCode);
    }
    [FirebirdPublicationFact] public async Task PrivateDataRequiresAnActiveAdminAndImportPreviewNeverWrites()
    {
        var (app, client) = await Host(); await using var dispose = app; using var http = client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/web/automacoes/chatfast/respostas")).StatusCode);
        http.DefaultRequestHeaders.Add("X-Test-Actor", "usuario");
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/api/web/automacoes/chatfast/respostas")).StatusCode);
        http.DefaultRequestHeaders.Remove("X-Test-Actor"); http.DefaultRequestHeaders.Add("X-Test-Actor", "admin");
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/web/automacoes/chatfast/respostas")).StatusCode);
        using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(ChatfastTests.Spreadsheet()), "file", "Tratados.xlsx"); form.Add(new StringContent("false"), "confirm");
        var response = await http.PostAsync("/api/web/automacoes/chatfast/importacoes", form); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var c = await factory.OpenConnectionAsync(); Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRESPOSTA"));
        await c.ExecuteAsync("UPDATE USUARIO SET STATUS=0 WHERE CODUSUARIO=1");
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/api/web/automacoes/chatfast/respostas")).StatusCode);
    }
    [FirebirdPublicationFact] public async Task ProductionRejectsUnencryptedProducerCalls()
    {
        var (app, client) = await Host(true); await using var dispose = app; using var http = client;
        http.DefaultRequestHeaders.Add("X-Chatfast-Key", new string('a', 64));
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/integracoes/chatfast/v1/respostas", ChatfastTests.Event())).StatusCode);
    }
    [FirebirdPublicationFact] public async Task DemoProjectsPublicDataKeepsPrivateGateAndNeverWritesSnapshots()
    {
        var path = Path.Combine(Path.GetTempPath(), "chatfast-demo-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(path, "diario.json"), JsonSerializer.Serialize(ChatfastExtendedTests.Report(), ChatfastContract.Json));
            var (app, client) = await Host(demoDirectory: path); await using var dispose = app; using var http = client;
            var json = await http.GetStringAsync("/api/publico/chatfast/v1/demonstracao/diario"); Assert.Contains("handledConversations", json); Assert.DoesNotContain("Pessoa reservada", json);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/web/automacoes/chatfast/demonstracao/diario")).StatusCode);
            http.DefaultRequestHeaders.Add("X-Test-Actor", "admin"); Assert.Contains("Pessoa reservada", await http.GetStringAsync("/api/web/automacoes/chatfast/demonstracao/diario"));
            await using var c = await factory.OpenConnectionAsync(); Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRELATORIO"));
            var (prod, prodClient) = await Host(true, path); await using var disposeProd = prod; using var production = prodClient;
            Assert.Equal(HttpStatusCode.NotFound, (await production.GetAsync("/api/publico/chatfast/v1/demonstracao/diario")).StatusCode);
        }
        finally { File.Delete(Path.Combine(path, "diario.json")); Directory.Delete(path); }
    }
    sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers["X-Test-Actor"].ToString(); if (actor.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, actor)], Scheme.Name)), Scheme.Name)));
        }
    }
}
