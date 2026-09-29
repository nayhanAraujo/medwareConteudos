using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Dapper;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;

namespace MdwConteudos.Api.Infrastructure;

public static class ProductionSecurity
{
    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownProxies.Clear();
            o.KnownIPNetworks.Clear();
            foreach (var proxy in builder.Configuration.GetSection("Security:TrustedProxies").Get<string[]>() ?? ["127.0.0.1", "::1"])
                o.KnownProxies.Add(IPAddress.Parse(proxy));
        });
        builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            var origins = builder.Configuration.GetSection("Security:CorsOrigins").Get<string[]>() ?? [];
            if (origins.Length > 0) p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }));
        var loginLimit = Positive(builder.Configuration, "Security:LoginPerMinute", 10);
        var studioLimit = Positive(builder.Configuration, "Security:StudioPerMinute", 10);
        var concurrency = Positive(builder.Configuration, "Security:StudioConcurrent", 2);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new { success = false, error = "Limite excedido", message = "Aguarde antes de tentar novamente." }, ct);
            };
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var path = context.Request.Path.Value?.TrimEnd('/');
                var authRequest = string.Equals(path, "/api/web/auth/login", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path, "/apiconteudos/v1/token", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path, "/api/v1/token", StringComparison.OrdinalIgnoreCase);
                return authRequest
                    ? RateLimitPartition.GetFixedWindowLimiter("auth:" + context.Connection.RemoteIpAddress, _ => Window(loginLimit))
                    : RateLimitPartition.GetNoLimiter("other");
            });
            options.AddPolicy("studio", context => RateLimitPartition.GetFixedWindowLimiter(UserKey(context), _ => Window(studioLimit)));
        });
        // Separate concurrency middleware holds the lease until the response completes.
        builder.Services.AddSingleton(PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetConcurrencyLimiter(UserKey(context), _ => new ConcurrencyLimiterOptions { PermitLimit = concurrency, QueueLimit = 0 })));
    }

    public static void Validate(WebApplication app)
    {
        if (!app.Environment.IsProduction()) return;
        ValidateConfiguration(app.Configuration,
            app.Services.GetRequiredService<IOptions<WebAuthOptions>>().Value,
            app.Services.GetRequiredService<IOptions<ApiPartnerOptions>>().Value,
            app.Services.GetRequiredService<IOptions<FirebirdOptions>>().Value,
            app.Services.GetRequiredService<IOptions<AssistantFirebirdOptions>>().Value);
        if (app.Configuration.GetValue<bool>("Security:AllowDefaultFirebirdPassword"))
            app.Logger.LogWarning("Security:AllowDefaultFirebirdPassword está habilitado. Restrinja o acesso às portas Firebird e planeje a rotação da senha padrão.");
        if (app.Configuration.GetValue<bool>("Security:AllowLegacyPartnerCredentials"))
            app.Logger.LogWarning("Security:AllowLegacyPartnerCredentials está habilitado. A API de parceiros mantém credenciais legadas para compatibilidade temporária.");
    }

    public static void ValidateConfiguration(IConfiguration config, WebAuthOptions web, ApiPartnerOptions partner,
        FirebirdOptions db, AssistantFirebirdOptions assistant)
    {
        Require(!web.DevUserEnabled, "WebAuth:DevUserEnabled deve estar desabilitado.");
        Require(Secret(web.JwtSecret) && Encoding.UTF8.GetByteCount(web.JwtSecret) >= 32, "WebAuth:JwtSecret deve ser próprio e ter ao menos 32 bytes.");
        var allowLegacyPartnerCredentials = config.GetValue<bool>("Security:AllowLegacyPartnerCredentials");
        var acceptedLegacyPartnerPair = allowLegacyPartnerCredentials
            && string.Equals(partner.JwtSecret, "mdw-api-jwt-conteudos-secret", StringComparison.Ordinal)
            && string.Equals(partner.JwtPassword, "Medware!111096", StringComparison.Ordinal);
        Require((Secret(partner.JwtSecret) && Secret(partner.JwtPassword)) || acceptedLegacyPartnerPair,
            "Credenciais próprias da API de parceiros são obrigatórias; para manter temporariamente o par legado do Python, habilite Security:AllowLegacyPartnerCredentials.");
        Require(web.JwtSecret != partner.JwtSecret && partner.JwtSecret != partner.JwtPassword, "Credenciais devem ser independentes.");
        Require(web.ExpirationMinutes > 0 && partner.JwtDatetimeToleranceHours > 0 && double.IsFinite(partner.JwtDatetimeToleranceHours), "Validades de autenticação inválidas.");
        Require(!string.IsNullOrWhiteSpace(web.Issuer) && !string.IsNullOrWhiteSpace(web.Audience), "Issuer e Audience são obrigatórios.");
        var allowDefaultFirebirdPassword = config.GetValue<bool>("Security:AllowDefaultFirebirdPassword");
        Require(FirebirdSecret(db.Password, allowDefaultFirebirdPassword) && FirebirdSecret(assistant.Password, allowDefaultFirebirdPassword),
            "Senhas próprias do Firebird são obrigatórias; para manter explicitamente masterkey, habilite Security:AllowDefaultFirebirdPassword.");
        Require(!string.IsNullOrWhiteSpace(db.Host) && !string.IsNullOrWhiteSpace(db.User) && db.Port is > 0 and <= 65535, "Conexão REFERENCIAS incompleta.");
        Require(!string.IsNullOrWhiteSpace(assistant.Host) && !string.IsNullOrWhiteSpace(assistant.User) && assistant.Port is > 0 and <= 65535, "Conexão ASSISTENTE incompleta.");
        Require(Path.IsPathFullyQualified(db.Database) && Path.IsPathFullyQualified(assistant.Database), "Caminhos absolutos dos bancos são obrigatórios.");
        var root = config["DataPaths:Root"];
        Require(!string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root) && Directory.Exists(root), "DataPaths:Root deve apontar para pasta persistente existente.");
        Require(config["AllowedHosts"] is { Length: > 0 } hosts && !hosts.Contains('*'), "AllowedHosts deve listar hosts explícitos.");
        Require(Uri.TryCreate(config["Documentation:PortalUrl"], UriKind.Absolute, out var portal) && portal.Scheme == "https" && !portal.IsLoopback, "Documentation:PortalUrl deve ser a URL HTTPS pública.");
        foreach (var origin in config.GetSection("Security:CorsOrigins").Get<string[]>() ?? [])
            Require(Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.GetLeftPart(UriPartial.Authority) == origin, "Origem CORS inválida.");
        Require(!config.GetValue<bool>("Documentation:AllowWrites"), "Escritas do console devem estar desabilitadas em produção.");
        var proxies = config.GetSection("Security:TrustedProxies").Get<string[]>();
        Require(proxies is { Length: > 0 } && proxies.All(p => IPAddress.TryParse(p, out var address) && IPAddress.IsLoopback(address)), "Security:TrustedProxies deve conter somente endereços loopback explícitos nesta instalação IIS.");
    }

    private static bool Secret(string? value) => !string.IsNullOrWhiteSpace(value)
        && !new[] { "masterkey", "mdw-api-jwt-conteudos-secret", "mdw-web-dev-secret-change-me-2026-local-migration-only", "Medware!111096", "altere-para-segredo-forte" }.Contains(value, StringComparer.OrdinalIgnoreCase)
        && !value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        && !value.StartsWith("homol-", StringComparison.OrdinalIgnoreCase);
    private static bool FirebirdSecret(string? value, bool allowDefault) =>
        Secret(value) || (allowDefault && string.Equals(value, "masterkey", StringComparison.OrdinalIgnoreCase));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Production: " + message); }
    private static int Positive(IConfiguration config, string key, int fallback)
    {
        var value = config.GetValue(key, fallback);
        if (value < 1) throw new InvalidOperationException(key + " deve ser maior que zero.");
        return value;
    }
    private static string UserKey(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous:" + context.Connection.RemoteIpAddress;
    private static FixedWindowRateLimiterOptions Window(int limit) => new() { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true };

    public static void MapHealth(WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
        app.MapGet("/health/ready", async (IFirebirdConnectionFactory db, IAssistantFirebirdConnectionFactory assistant, CancellationToken ct) =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await using var first = await db.OpenConnectionAsync(timeout.Token);
                await first.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1 FROM RDB$DATABASE", commandTimeout: 5, cancellationToken: timeout.Token));
                await using var second = await assistant.OpenConnectionAsync(timeout.Token);
                await second.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1 FROM RDB$DATABASE", commandTimeout: 5, cancellationToken: timeout.Token));
                return Results.Ok(new { status = "ready" });
            }
            catch { return Results.Json(new { status = "unavailable" }, statusCode: 503); }
        });
    }
}

public sealed class StudioConcurrencyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PartitionedRateLimiter<HttpContext> limiter)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName != "studio")
        { await next(context); return; }
        using var lease = await limiter.AcquireAsync(context, 1, context.RequestAborted);
        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Limite de operações simultâneas atingido." });
            return;
        }
        await next(context);
    }
}
