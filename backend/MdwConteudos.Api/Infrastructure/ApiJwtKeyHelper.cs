using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MdwConteudos.Api.Infrastructure;

public static class ApiJwtKeyHelper
{
    public static byte[] GetKeyBytes(string secret)
    {
        // PyJWT uses the UTF-8 secret verbatim, including keys shorter than 256 bits.
        return Encoding.UTF8.GetBytes(secret ?? "");
    }

    public static string CreateToken(object payload, string secret)
    {
        var header = EncodeBase64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"u8);
        var body = EncodeBase64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{header}.{body}";
        var signature = HMACSHA256.HashData(GetKeyBytes(secret), Encoding.ASCII.GetBytes(signingInput));
        return $"{signingInput}.{EncodeBase64Url(signature)}";
    }

    public static JsonElement VerifyToken(string token, string secret, bool acceptLegacyHashedKey = false)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) throw new FormatException("JWT must have three segments.");
        using var header = JsonDocument.Parse(DecodeBase64Url(parts[0]));
        if (header.RootElement.ValueKind != JsonValueKind.Object
            || !header.RootElement.TryGetProperty("alg", out var alg)
            || alg.ValueKind != JsonValueKind.String || alg.GetString() != "HS256")
            throw new FormatException("Only HS256 is accepted.");
        if (header.RootElement.TryGetProperty("b64", out var b64) && b64.ValueKind == JsonValueKind.False)
            throw new FormatException("Detached payloads are not accepted.");

        var signature = DecodeBase64Url(parts[2]);
        var raw = GetKeyBytes(secret);
        var input = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        var valid = CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(raw, input), signature);
        // Only short keys were hashed by the previous .NET implementation.
        if (acceptLegacyHashedKey && raw.Length < 32)
            valid |= CryptographicOperations.FixedTimeEquals(
                HMACSHA256.HashData(SHA256.HashData(raw), input), signature);
        if (!valid) throw new CryptographicException("Invalid JWT signature.");

        using var payload = JsonDocument.Parse(DecodeBase64Url(parts[1]));
        if (payload.RootElement.ValueKind != JsonValueKind.Object)
            throw new FormatException("JWT payload must be an object.");
        return payload.RootElement.Clone();
    }

    private static string EncodeBase64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DecodeBase64Url(string value)
    {
        if (value.Length == 0 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '='))
            throw new FormatException("Invalid base64url segment.");
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
