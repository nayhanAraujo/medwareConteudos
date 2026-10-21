using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MdwConteudos.Api.Modules.Auth;

namespace ConversorHtml.Tests;

public sealed class PasswordRecoveryTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    [InlineData("Name <name@example.test>", false)]
    [InlineData("a b@example.test", false)]
    [InlineData("name@example.test", true)]
    public void EmailValidation(string? email, bool valid) => Assert.Equal(valid, RecoveryEmail.IsValid(email));

    [Fact]
    public void EmailNormalization() => Assert.Equal("name@example.test", RecoveryEmail.Normalize(" Name@Example.Test "));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RequestReturnsSameConfirmationWithoutLeakingAccount(bool exists)
    {
        var store = new Store { Exists = exists };
        var sender = new Sender();
        var controller = new AuthController(null!, null!, Service(store, sender));
        var result = Assert.IsType<OkObjectResult>(await controller.ForgotPassword(new(" NAME@example.test "), default));
        Assert.Equal(PasswordRecoveryService.Confirmation, JsonSerializer.SerializeToElement(result.Value).GetProperty("message").GetString());
        Assert.Equal("name@example.test", store.Email);
        Assert.Equal(exists ? 1 : 0, sender.Count);
        if (exists)
        {
            Assert.Matches("^[0-9a-f]{64}$", sender.Token!);
            Assert.NotEqual(sender.Token, store.Hash);
            Assert.Equal(MdwConteudos.Api.Infrastructure.PasswordHasher.Sha256Hex(sender.Token!), store.Hash);
        }
    }

    [Fact]
    public async Task SmtpFailureInvalidatesTokenAndStillConfirms()
    {
        var store = new Store();
        var controller = new AuthController(null!, null!, Service(store, new Sender { Fail = true }));
        Assert.IsType<OkObjectResult>(await controller.ForgotPassword(new("name@example.test"), default));
        Assert.Equal(store.Hash, store.InvalidatedHash);
    }

    [Theory]
    [InlineData("12345", "12345")]
    [InlineData("      ", "      ")]
    [InlineData("123456", "other")]
    public async Task InvalidPasswordsNeverReachStore(string password, string confirmation)
    {
        var store = new Store();
        var controller = new AuthController(null!, null!, Service(store, new Sender()));
        Assert.IsType<BadRequestObjectResult>(await controller.ResetPassword(new(new string('a', 64), password, confirmation), default));
        Assert.Equal(0, store.Redeemed);
    }

    [Fact]
    public async Task InvalidEmailAndMalformedTokenNeverReachStore()
    {
        var store = new Store();
        var controller = new AuthController(null!, null!, Service(store, new Sender()));
        Assert.IsType<BadRequestObjectResult>(await controller.ForgotPassword(new("invalid"), default));
        Assert.IsType<BadRequestObjectResult>(await controller.ResetPassword(new("tampered", "123456", "123456"), default));
        Assert.Null(store.Hash);
        Assert.Equal(0, store.Redeemed);
    }

    private static PasswordRecoveryService Service(Store store, Sender sender) => new(store, sender, NullLogger<PasswordRecoveryService>.Instance, TimeProvider.System);
    private sealed class Store : IPasswordRecoveryStore
    {
        public bool Exists = true;
        public string? Email, Hash, InvalidatedHash;
        public int Redeemed;
        public Task<string?> IssueAsync(string email, string hash, DateTime now, CancellationToken ct)
        { Email = email; Hash = hash; return Task.FromResult(Exists ? email : null); }
        public Task InvalidateAsync(string hash, DateTime now, CancellationToken ct) { InvalidatedHash = hash; return Task.CompletedTask; }
        public Task<bool> RedeemAsync(string hash, string passwordHash, DateTime now, CancellationToken ct) { Redeemed++; return Task.FromResult(true); }
    }
    private sealed class Sender : IRecoveryEmailSender
    {
        public string? Token;
        public int Count;
        public bool Fail;
        public Task SendAsync(string email, string token, CancellationToken ct)
        { Count++; Token = token; if (Fail) throw new InvalidOperationException("SMTP failure"); return Task.CompletedTask; }
    }
}
