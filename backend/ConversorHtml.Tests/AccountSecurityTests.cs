using System.Security.Claims;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Auth;
using MdwConteudos.Api.Modules.Users;

namespace ConversorHtml.Tests;

public class AccountSecurityTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData("Homologacao", false)]
    [InlineData(null, false)]
    public void DevBypassRequiresExplicitDevelopmentEnvironment(string? environment, bool expected)
    {
        var service = new AuthService(new NoDatabase(), Options.Create(new WebAuthOptions
        {
            DevUserEnabled = true, DevUsername = "dev", DevPassword = "secret"
        }), environment is null ? null : new TestEnvironment { EnvironmentName = environment });
        var method = typeof(AuthService).GetMethod("IsDevUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.Equal(expected, method.Invoke(service, new object[] { "dev", "secret" }));
    }

    [Fact]
    public async Task AnonymousRecoveryNeverCallsServiceOrDatabase()
    {
        var controller = new AuthController(null!, null!);
        var result = Assert.IsType<ObjectResult>(await controller.ForgotPassword(null!, default));
        Assert.Equal(410, result.StatusCode);
        var service = new AuthService(new NoDatabase(), Options.Create(new WebAuthOptions()));
        Assert.False((await service.ForgotPasswordAsync("anyone", "secret")).Ok);
    }

    [Theory]
    [InlineData("admin", "usuario", null)]
    [InlineData("AdMiN", "usuario", "secret")]
    [InlineData("admin", "admin", null)]
    [InlineData("usuario", "ADMIN", null)]
    [InlineData("usuario", "usuario", "secret")]
    public async Task EditorCannotTakeOverAdminPromoteOrResetOthers(string current, string requested, string? password)
    {
        var users = new FakeUsers(current);
        var controller = Controller(users, "usuario");
        Assert.IsType<ForbidResult>(await controller.Update(2, Request(requested, password), default));
        Assert.Equal(0, users.Writes);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    public async Task NonAdminAndStaleAdminCannotCreateOrDeleteAdmin(string claim)
    {
        var users = new FakeUsers("ADMIN");
        var controller = Controller(users, claim);
        Assert.IsType<ForbidResult>(await controller.Create(new("Admin", "admin", "secret", "secret", "AdMiN", -1), default));
        Assert.IsType<ForbidResult>(await controller.Delete(2, default));
        Assert.IsType<ForbidResult>(await controller.Update(2, Request("usuario", "secret"), default));
        Assert.Equal(0, users.Writes);
    }

    [Fact]
    public async Task ActiveAdminCanResetAndAuditContainsOnlyIdsAndOutcome()
    {
        var users = new FakeUsers("admin", "AdMiN");
        var logger = new AuditLogger();
        var controller = Controller(users, "ADMIN", logger);
        Assert.IsType<OkObjectResult>(await controller.Update(2, Request("admin", "private-password"), default));
        Assert.Equal(1, users.Writes);
        var entry = Assert.Single(logger.Messages);
        Assert.Contains("Actor=1 Target=2 Outcome=success", entry);
        Assert.DoesNotContain("private-password", entry);
    }

    [Fact]
    public async Task DeniedResetIsAudited()
    {
        var logger = new AuditLogger();
        await Controller(new FakeUsers("usuario"), "usuario", logger).Update(2, Request("usuario", "secret"), default);
        Assert.Contains("Outcome=denied", Assert.Single(logger.Messages));
    }

    [Fact]
    public async Task EditorCanEditOrdinaryAccountWithoutPassword()
    {
        var users = new FakeUsers("usuario");
        Assert.IsType<OkObjectResult>(await Controller(users, "usuario").Update(2, Request("comum", null), default));
        Assert.Equal(1, users.Writes);
    }

    [Theory]
    [InlineData("admin", "12345", "12345")]
    [InlineData("invalid", null, null)]
    [InlineData("usuario", "123456", "mismatch")]
    [InlineData("usuario", null, "123456")]
    public async Task InvalidUpdatesRejectedBeforeDatabase(string profile, string? password, string? confirmation)
    {
        var service = new UsersService(new NoDatabase());
        Assert.False((await service.UpdateAsync(2, Request(profile, password) with { ConfirmarSenha = confirmation })).Ok);
    }

    private static UpdateUserRequest Request(string profile, string? password) => new("Name", "login", password, password, profile, -1);

    private static UsersController Controller(FakeUsers users, string role, ILogger<UsersController>? logger = null) => new(users, logger)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role)
                }, "test"))
            }
        }
    };

    private sealed class NoDatabase : IFirebirdConnectionFactory
    {
        public Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default) => throw new InvalidOperationException("Database must not be accessed");
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeUsers(string targetRole, string actorRole = "usuario") : IUsersService
    {
        public int Writes { get; private set; }
        public Task<UserListItem?> GetAsync(int id, CancellationToken ct = default) => Task.FromResult<UserListItem?>(new(id, "Name", "login", id == 1 ? actorRole : targetRole, -1));
        public Task<IEnumerable<UserListItem>> ListAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(bool Ok, string? Error)> CreateAsync(CreateUserRequest req, CancellationToken ct = default) { Writes++; return Task.FromResult<(bool, string?)>((true, null)); }
        public Task<(bool Ok, string? Error)> UpdateAsync(int id, UpdateUserRequest req, CancellationToken ct = default) { Writes++; return Task.FromResult<(bool, string?)>((true, null)); }
        public Task<bool> DeleteAsync(int id, CancellationToken ct = default) { Writes++; return Task.FromResult(true); }
    }

    private sealed class AuditLogger : ILogger<UsersController>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
