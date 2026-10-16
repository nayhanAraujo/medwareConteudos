using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.ApiPublica;
using MdwConteudos.Api.Modules.Avaliacoes;
using MdwConteudos.Api.Modules.Permissions;

namespace ConversorHtml.Tests;

public sealed class AvaliacaoAuthTests
{
    [Theory]
    [InlineData("/apiconteudos/v1/scripts/1/avaliacoes", "PUT")]
    [InlineData("/apiconteudos/v1/scripts/1/avaliacoes/resumo", "GET")]
    [InlineData("/apiconteudos/v1/scripts/1/avaliacoes/minha", "GET")]
    [InlineData("/api/v1/scripts/1/avaliacoes", "PUT")]
    public async Task PartnerTokenIsRequired(string path, string verb)
    {
        const string secret = "synthetic-test-secret-evaluations-2026";
        var valid = ApiJwtKeyHelper.CreateToken(new { senha = "test-password", datahora = DateTime.UtcNow.ToString("O") }, secret);
        foreach (var token in new string?[] { null, "invalid", valid })
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path; context.Request.Method = verb; context.Response.Body = new MemoryStream();
            if (token is not null) context.Request.Headers.Authorization = "Bearer " + token;
            var reached = false;
            var middleware = new ApiPartnerJwtMiddleware(_ => { reached = true; return Task.CompletedTask; },
                Options.Create(new ApiPartnerOptions { JwtSecret = secret, JwtPassword = "test-password" }));
            await middleware.InvokeAsync(context);
            Assert.Equal(token == valid, reached);
            Assert.Equal(token == valid ? 200 : 401, context.Response.StatusCode);
        }
    }

    [Theory]
    [InlineData("GET", "visualizar")]
    [InlineData("PUT", "acompanhar")]
    public async Task WebAliasChecksEvaluationDomain(string verb, string action)
    {
        var permission = DispatchProxy.Create<IPermissionService, PermissionProxy>();
        var proxy = (PermissionProxy)permission;
        using var services = new ServiceCollection().AddSingleton(permission).BuildServiceProvider();
        foreach (var allowed in new[] { false, true })
        {
            proxy.Allow = allowed;
            var context = new DefaultHttpContext { RequestServices = services };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "usuario") }, "test"));
            context.Request.Path = "/api/v1/scripts/1/avaliacoes"; context.Request.Method = verb; context.Response.Body = new MemoryStream();
            var reached = false;
            await new ApiPartnerJwtMiddleware(_ => { reached = true; return Task.CompletedTask; }, Options.Create(new ApiPartnerOptions())).InvokeAsync(context);
            Assert.Equal(allowed, reached);
            Assert.Equal("avaliacoes", proxy.Domain); Assert.Equal(action, proxy.Action);
            Assert.Equal(allowed ? 200 : 403, context.Response.StatusCode);
        }
    }

    [Fact]
    public void CentralOperationsHaveSeparatePermissionsAndNoDelete()
    {
        foreach (var (method, action) in new[] { ("List", "visualizar"), ("Summary", "visualizar"), ("Detail", "visualizar"), ("Follow", "acompanhar"), ("Note", "acompanhar"), ("Visibility", "moderar") })
        {
            var guard = typeof(AvaliacaoWebController).GetMethod(method)!.GetCustomAttribute<RequirePermissionAttribute>()!;
            Assert.Equal(new object[] { "avaliacoes", action }, guard.Arguments);
        }
        Assert.DoesNotContain(typeof(AvaliacaoWebController).GetMethods(), method => method.GetCustomAttribute<HttpDeleteAttribute>() is not null);
    }

    public class PermissionProxy : DispatchProxy
    {
        public bool Allow { get; set; }
        public string? Domain { get; private set; }
        public string? Action { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Domain = (string)args![2]!; Action = (string)args[3]!;
            return Task.FromResult(Allow);
        }
    }
}
