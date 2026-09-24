using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.Permissions;

namespace ConversorHtml.Tests;

public class InternalApiSecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly ApiPartnerOptions OptionsValue = new() { JwtSecret = "fixture-secret", JwtPassword = "fixture-password" };
    [Theory]
    [InlineData("/api/v1/variaveis", "GET")]
    [InlineData("/api/v1/normalidades/1", "PUT")]
    [InlineData("/API/V1/variaveis/", "GET")]
    public async Task Anonymous_internal_operations_are_denied(string path, string method) =>
        Assert.Equal(401, (await Invoke(path, method)).Response.StatusCode);
    [Fact]
    public async Task Partner_token_authenticates_but_never_creates_web_identity()
    {
        var token = ApiJwtKeyHelper.CreateToken(new { senha = "fixture-password", datahora = "2026-09-23T12:00:00Z" }, "fixture-secret");
        var context = await Invoke("/api/v1/variaveis", "GET", token);
        Assert.Equal(204, context.Response.StatusCode);
        Assert.False(context.User.Identity?.IsAuthenticated == true);
    }
    [Theory]
    [InlineData(false, "GET", 403, "visualizar")]
    [InlineData(true, "GET", 204, "visualizar")]
    [InlineData(false, "PUT", 403, "editar")]
    [InlineData(true, "PUT", 204, "editar")]
    public async Task Web_identity_requires_domain_and_action(bool allowed, string method, int status, string action)
    {
        var permissions = new Permissions(allowed);
        var context = await Invoke("/api/v1/normalidades/1", method, permissions: permissions);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(("variaveis", action), permissions.Requested);
    }
    private static async Task<DefaultHttpContext> Invoke(string path, string method, string? token = null, Permissions? permissions = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path; context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        if (token is not null) context.Request.Headers.Authorization = "Bearer " + token;
        using var provider = new ServiceCollection().AddSingleton<IPermissionService>(permissions ?? new(false)).BuildServiceProvider();
        context.RequestServices = provider;
        if (permissions is not null) context.User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "1"), new(ClaimTypes.Role, "usuario")], "web"));
        var middleware = new ApiPartnerJwtMiddleware(c => { c.Response.StatusCode = 204; return Task.CompletedTask; }, Options.Create(OptionsValue), new ApiV1FixedTimeProvider(Now));
        await middleware.InvokeAsync(context);
        return context;
    }
    private sealed class Permissions(bool allowed) : IPermissionService
    {
        public (string, string) Requested;
        public Task<bool> HasPermissionAsync(int id, string role, string domain, string action, CancellationToken ct = default) { Requested = (domain, action); return Task.FromResult(allowed); }
        public Task EnsureSchemaAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PermissionCatalogItem>> GetCatalogAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProfilePermissionState> GetProfileAsync(string perfil, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetProfileAsync(string perfil, IReadOnlyList<string> keys, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserPermissionState> GetUserAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetUserAsync(int id, IReadOnlyList<UserPermissionOverride> items, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int id, string role, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
