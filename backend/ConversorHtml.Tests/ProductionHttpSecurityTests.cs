using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MdwConteudos.Api.Infrastructure;

namespace ConversorHtml.Tests;

public class ProductionHttpSecurityTests
{
    [Fact]
    public async Task Login_throttle_returns_429_and_retry_after_over_real_HTTP()
    {
        await using var app = Build();
        app.MapPost("/api/web/auth/login", () => Results.Unauthorized());
        await app.StartAsync();
        using var client = Client(app);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/web/auth/login", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/web/auth/login", null)).StatusCode);
        using var limited = await client.PostAsync("/api/web/auth/login", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task Cors_allows_only_explicit_origin_without_credentials()
    {
        await using var app = Build();
        app.MapGet("/read", () => Results.Ok());
        await app.StartAsync();
        using var client = Client(app);
        foreach (var origin in new[] { "https://allowed.test", "https://other.test" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/read");
            request.Headers.Add("Origin", origin);
            using var response = await client.SendAsync(request);
            Assert.Equal(origin == "https://allowed.test", response.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        }
    }

    [Fact]
    public async Task Studio_concurrency_lease_is_released_and_partitioned_by_user()
    {
        await using var app = Build();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.MapPost("/work", async (HttpContext context) =>
        {
            if (context.Request.Query.ContainsKey("wait")) { started.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
            return Results.Ok();
        }).RequireRateLimiting("studio");
        await app.StartAsync();
        using var client = Client(app);
        client.DefaultRequestHeaders.Add("X-Test-User", "1");
        var first = client.PostAsync("/work?wait=1", null);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/work", null)).StatusCode);
            using var other = Client(app);
            other.DefaultRequestHeaders.Add("X-Test-User", "2");
            Assert.Equal(HttpStatusCode.OK, (await other.PostAsync("/work", null)).StatusCode);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/work", null)).StatusCode);
    }

    private static WebApplication Build()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = Path.GetTempPath() });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:LoginPerMinute"] = "2", ["Security:StudioConcurrent"] = "1",
            ["Security:StudioPerMinute"] = "50", ["Security:CorsOrigins:0"] = "https://allowed.test"
        });
        ProductionSecurity.Configure(builder);
        var app = builder.Build();
        app.UseRouting();
        app.UseCors();
        // Test-only identity injection; production obtains it from verified JWTs.
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-User", out var id))
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id.ToString())], "test"));
            await next();
        });
        app.UseRateLimiter();
        app.UseMiddleware<StudioConcurrencyMiddleware>();
        return app;
    }
    private static HttpClient Client(WebApplication app) => new()
    {
        BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
        Timeout = TimeSpan.FromSeconds(15)
    };
}
