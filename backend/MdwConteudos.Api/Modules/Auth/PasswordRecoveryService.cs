using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Auth;

public static class RecoveryEmail
{
    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    public static bool IsValid(string? value) => value is { Length: <= 254 } &&
        MailAddress.TryCreate(value, out var address) && address.Address == value && !value.Any(c => char.IsWhiteSpace(c) || c > 127);
}

public interface IPasswordRecoveryStore
{
    Task<string?> IssueAsync(string email, string hash, DateTime now, CancellationToken ct);
    Task InvalidateAsync(string hash, DateTime now, CancellationToken ct);
    Task<bool> RedeemAsync(string hash, string passwordHash, DateTime now, CancellationToken ct);
}

public interface IRecoveryEmailSender
{
    Task SendAsync(string email, string token, CancellationToken ct);
}

public interface IPasswordRecoveryService
{
    Task RequestAsync(string email, CancellationToken ct);
    Task<bool> ResetAsync(string token, string password, CancellationToken ct);
}

public sealed partial class PasswordRecoveryService(
    IPasswordRecoveryStore store, IRecoveryEmailSender sender,
    ILogger<PasswordRecoveryService> logger, TimeProvider clock) : IPasswordRecoveryService
{
    public const string Confirmation = "Se houver uma conta ativa com esse e-mail, enviaremos as instruções de recuperação.";
    public const string InvalidLink = "Link inválido, expirado ou já utilizado. Solicite um novo link de recuperação.";

    public async Task RequestAsync(string email, CancellationToken ct)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var hash = PasswordHasher.Sha256Hex(token);
        var recipient = await store.IssueAsync(email, hash, clock.GetUtcNow().UtcDateTime, ct);
        if (recipient is null) return;
        try { await sender.SendAsync(recipient, token, ct); }
        catch
        {
            // SMTP exceptions may contain the recipient. Never log them or the link.
            logger.LogWarning("PasswordRecoveryEmail Outcome=failed");
            await store.InvalidateAsync(hash, clock.GetUtcNow().UtcDateTime, CancellationToken.None);
        }
    }

    public Task<bool> ResetAsync(string token, string password, CancellationToken ct) =>
        TokenPattern().IsMatch(token)
            ? store.RedeemAsync(PasswordHasher.Sha256Hex(token), PasswordHasher.Sha256Hex(password), clock.GetUtcNow().UtcDateTime, ct)
            : Task.FromResult(false);

    [GeneratedRegex("\\A[0-9a-f]{64}\\z")]
    private static partial Regex TokenPattern();
}
