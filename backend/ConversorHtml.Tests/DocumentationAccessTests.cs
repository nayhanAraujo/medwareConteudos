using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MdwConteudos.Api.Modules.Documentation;

namespace ConversorHtml.Tests;

public class DocumentationAccessTests
{
    [Theory]
    [InlineData("/swagger/apiconteudos/swagger.json", null, 200, true)]
    [InlineData("/swagger/api-v1/swagger.json", null, 401, false)]
    [InlineData("/swagger/web/swagger.json", null, 401, false)]
    [InlineData("/swagger/web/swagger.json", "usuario", 403, false)]
    [InlineData("/swagger/api-v1/swagger.json", "admin", 200, true)]
    [InlineData("/swagger/web/swagger.json", "admin", 200, true)]
    public async Task Swagger_documents_enforce_server_side_role(string path, string? role, int status, bool expectedNext)
    {
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new Auth(role)).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        var nextCalled = false;
        await new DocumentationAccessMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(expectedNext, nextCalled);
        if (role == "admin") Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    private sealed class Auth(string? role) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            Assert.Equal(JwtBearerDefaults.AuthenticationScheme, scheme);
            return Task.FromResult(role is null ? AuthenticateResult.NoResult() : AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test")), "Bearer")));
        }
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
