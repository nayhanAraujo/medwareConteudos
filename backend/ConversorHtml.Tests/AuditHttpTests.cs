using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Auditoria;

namespace ConversorHtml.Tests;

public sealed class AuditHttpTests
{
    [FirebirdPublicationFact]
    public async Task RealHttpEnvelopesAuthorizationAndValidationUseTemporaryDatabase()
    {
        var cs = new FbConnectionStringBuilder {
            Database = Path.Combine(Path.GetTempPath(), $"audit-http-{Guid.NewGuid():N}.fdb"),
            ServerType = FbServerType.Embedded, ClientLibrary = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"),
            UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false
        }.ToString();
        FbConnection.CreateDatabase(cs);
        try
        {
            var accessor = new HttpContextAccessor();
            var factory = new Factory(cs, accessor);
            await using (var conn = await factory.OpenConnectionAsync())
            {
                await conn.ExecuteAsync("CREATE TABLE USUARIO(CODUSUARIO INTEGER PRIMARY KEY,NOME VARCHAR(100),STATUS INTEGER,PERFIL VARCHAR(20))");
                await conn.ExecuteAsync("INSERT INTO USUARIO VALUES (1,'Admin',-1,'admin')");
                await conn.ExecuteAsync("INSERT INTO USUARIO VALUES (2,'Normal',-1,'usuario')");
                await conn.ExecuteAsync("INSERT INTO USUARIO VALUES (3,'Inactive',0,'admin')");
                await conn.ExecuteAsync("CREATE TABLE PACOTES(CODPACOTE INTEGER PRIMARY KEY,NOME VARCHAR(100))");
            }
            await AuditSchema.MigrateAsync(factory);
            var seedContext = new DefaultHttpContext();
            seedContext.Items[AuditRequest.ItemKey] = new AuditRequest();
            await using (var conn = await factory.OpenConnectionAsync())
            {
                await AuditSession.InitializeAsync(conn, seedContext, default);
                await conn.ExecuteAsync("INSERT INTO PACOTES VALUES(1,'Item')");
                await conn.ExecuteAsync("INSERT INTO PACOTES VALUES(2,'Other')");
            }
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton<IHttpContextAccessor>(accessor);
            builder.Services.AddSingleton<IFirebirdConnectionFactory>(factory);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddScoped<AuditReader>();
            builder.Services.AddScoped<AuditWriter>();
            builder.Services.AddScoped<ActiveAuditAdminFilter>();
            builder.Services.AddAuthentication("AuditTest").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("AuditTest", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers(o => o.Filters.Add<AuditFileResultFilter>()).AddApplicationPart(typeof(AuditoriaController).Assembly);
            await using var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseMiddleware<AuditMiddleware>(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            try
            {
                var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                using var client = new HttpClient { BaseAddress = new Uri(address) };
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/web/auditoria")).StatusCode);
                client.DefaultRequestHeaders.Add("X-Test-User", "2");
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/web/auditoria/filtros")).StatusCode);
                client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", "3");
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/web/auditoria/1")).StatusCode);
                client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", "1");
                var response = await client.GetAsync("/api/web/auditoria?module=pacotes&take=1");
                response.EnsureSuccessStatusCode();
                using var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.True(page.RootElement.GetProperty("success").GetBoolean());
                var item = Assert.Single(page.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal(JsonValueKind.String, item.GetProperty("id").ValueKind);
                Assert.EndsWith("Z", item.GetProperty("occurredAtUtc").GetString());
                Assert.False(item.TryGetProperty("changes", out _));
                var cursor = page.RootElement.GetProperty("nextCursor").GetString();
                Assert.NotNull(cursor);
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/web/auditoria?module=pacotes&take=1&cursor=" + Uri.EscapeDataString(cursor))).StatusCode);
                using var detail = JsonDocument.Parse(await client.GetStringAsync("/api/web/auditoria/" + item.GetProperty("id").GetString()));
                Assert.True(detail.RootElement.GetProperty("data").GetProperty("changes").GetArrayLength() > 0);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/web/auditoria?cursor=invalid")).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/web/auditoria?take=1000")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/web/auditoria/9223372036854775806")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/web/auditoria/filtros")).StatusCode);
            }
            finally { await app.StopAsync(); }
        }
        finally { FbConnection.DropDatabase(cs); }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!int.TryParse(Request.Headers["X-Test-User"], out var id)) return Task.FromResult(AuthenticateResult.NoResult());
            // Every test token falsely claims admin: the database must decide.
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(ClaimTypes.Role,"admin")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
    private sealed class Factory(string cs, IHttpContextAccessor http) : IFirebirdConnectionFactory
    {
        public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default)
        {
            var conn = new FbConnection(cs); await conn.OpenAsync(ct);
            await AuditSession.InitializeAsync(conn, http.HttpContext, ct);
            return conn;
        }
    }
}
