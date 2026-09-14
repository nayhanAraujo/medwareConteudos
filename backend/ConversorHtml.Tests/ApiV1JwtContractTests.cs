using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.ApiPublica;

namespace ConversorHtml.Tests;

public class ApiV1JwtContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
    private const string Password = "senha-fixture";
    public static IEnumerable<object[]> PythonCases => ApiV1TestSupport.Cases("auth");

    [Theory]
    [MemberData(nameof(PythonCases))]
    public async Task Middleware_matches_executed_Python_reference(string name, string json)
    {
        using var fixture = JsonDocument.Parse(json);
        var item = fixture.RootElement;
        var options = OptionsFor(item.GetProperty("secret").GetString()!);
        var response = await Invoke(item.GetProperty("authorization").GetString(), options,
            DateTimeOffset.Parse(item.GetProperty("now").GetString()!, CultureInfo.InvariantCulture));
        Assert.Equal(item.GetProperty("status").GetInt32(), response.StatusCode);
        if (response.StatusCode == 200)
            Assert.Equal(0, response.Body.Length);
        else
        {
            Assert.Equal("application/json", response.ContentType);
            response.Body.Position = 0;
            ApiV1TestSupport.EqualJson(JsonNode.Parse(item.GetProperty("body").GetRawText()),
                await JsonNode.ParseAsync(response.Body));
        }
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("çã🔑")]
    [InlineData("01234567890123456789012345678901")]
    [InlineData("0123456789012345678901234567890123456789")]
    public void Key_is_raw_utf8_and_signing_is_independent_HMACSHA256(string secret)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(secret), ApiJwtKeyHelper.GetKeyBytes(secret));
        var token = ApiJwtKeyHelper.CreateToken(new { senha = Password, datahora = "2026-09-08T12:00:00Z" }, secret);
        var parts = token.Split('.');
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"));
        Assert.Equal(Base64Url(signature), parts[2]);
        Assert.Equal(Password, ApiJwtKeyHelper.VerifyToken(token, secret).GetProperty("senha").GetString());
    }

    [Theory]
    [InlineData(false, "2026-09-09T00:00:00Z", 401)]
    [InlineData(true, null, 401)]
    [InlineData(true, "", 401)]
    [InlineData(true, "2026-09-09T00:00:00", 401)]
    [InlineData(true, "2026-09-09T00:00:00-03:00", 401)]
    [InlineData(true, "inválido", 401)]
    [InlineData(true, "2026-09-08T11:59:59Z", 401)]
    [InlineData(true, "2026-09-08T12:00:00Z", 401)]
    [InlineData(true, "2026-09-08T12:00:00.000001Z", 200)]
    [InlineData(true, "2026-09-09T00:00:00+00:00", 200)]
    public async Task Old_hashed_key_requires_opt_in_and_unexpired_explicit_UTC_deadline(bool enabled, string? until, int status)
    {
        var options = OptionsFor();
        options.AcceptLegacyHashedKey = enabled;
        options.JwtLegacyHashedKeyUntilUtc = until;
        var token = ApiV1TestSupport.Reference.GetProperty("legacy_hashed_token").GetString();
        Assert.Equal(status, (await Invoke(token, options)).StatusCode);
        // Deadline only controls the old signature; the raw Python signature remains valid.
        Assert.Equal(200, (await Invoke(Sign(new { senha = Password, datahora = "2026-09-08T12:00:00Z" }), options)).StatusCode);
    }

    [Fact]
    public async Task Legacy_acceptance_is_disabled_by_default()
    {
        var options = OptionsFor();
        Assert.False(options.AcceptLegacyHashedKey);
        Assert.Null(options.JwtLegacyHashedKeyUntilUtc);
        Assert.Equal(401, (await Invoke(ApiV1TestSupport.Reference.GetProperty("legacy_hashed_token").GetString(), options)).StatusCode);
    }

    [Fact]
    public async Task Issuer_returns_exact_Python_token_even_during_migration_and_does_not_require_exp()
    {
        var options = OptionsFor();
        options.AcceptLegacyHashedKey = true;
        options.JwtLegacyHashedKeyUntilUtc = "2026-09-09T00:00:00Z";
        var service = Service(options);
        var result = Assert.IsType<OkObjectResult>(await service.ObterTokenAsync(new TokenRequest("", Password), default));
        var body = JsonSerializer.SerializeToElement(result.Value);
        var fixtureToken = ApiV1TestSupport.Reference.GetProperty("auth")[0].GetProperty("authorization").GetString()![7..];
        Assert.Equal(fixtureToken, body.GetProperty("token").GetString());
        Assert.Equal(new[] { "expires_info", "success", "token" }, body.EnumerateObject().Select(p => p.Name).Order().ToArray());
        var payload = ApiJwtKeyHelper.VerifyToken(body.GetProperty("token").GetString()!, options.JwtSecret);
        Assert.False(payload.TryGetProperty("exp", out _));
        Assert.Equal("2026-09-08T12:00:00Z", payload.GetProperty("datahora").GetString());
    }

    [Theory]
    [InlineData(null, null, 400, "Senha não informada")]
    [InlineData("errada", Password, 401, "Não autorizado")]
    public async Task Token_endpoint_preserves_legacy_errors(string? senha, string? alias, int status, string error)
    {
        var result = Assert.IsAssignableFrom<ObjectResult>(await Service(OptionsFor()).ObterTokenAsync(new TokenRequest(senha, alias), default));
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(error, JsonSerializer.SerializeToElement(result.Value).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Missing_configuration_is_reported_after_signature_validation()
    {
        var options = OptionsFor();
        options.JwtPassword = "";
        Assert.Equal(401, (await Invoke("malformed", options)).StatusCode);
        Assert.Equal(503, (await Invoke(Sign(new { senha = Password, datahora = "2026-09-08T12:00:00Z" }), options)).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await Service(options).ObterTokenAsync(new TokenRequest(Password, null), default)).StatusCode);
    }

    [Fact]
    public async Task Tampering_every_signature_byte_is_rejected()
    {
        var token = Sign(new { senha = Password, datahora = "2026-09-08T12:00:00Z" });
        var parts = token.Split('.');
        var signature = Convert.FromBase64String(parts[2].Replace('-', '+').Replace('_', '/') + "=");
        for (var index = 0; index < signature.Length; index++)
        {
            var changed = (byte[])signature.Clone();
            changed[index] ^= 1;
            Assert.Equal(401, (await Invoke($"{parts[0]}.{parts[1]}.{Base64Url(changed)}", OptionsFor())).StatusCode);
        }
    }

    [Theory]
    [InlineData("/api/v1/variaveis", "GET")]
    [InlineData("/apiconteudos/v10/variaveis", "GET")]
    [InlineData("/apiconteudos/v1/health", "GET")]
    [InlineData("/apiconteudos/v1/token", "POST")]
    [InlineData("/apiconteudos/v1/variaveis", "OPTIONS")]
    public async Task Middleware_is_scoped_and_preserves_public_exceptions(string path, string method) =>
        Assert.Equal(200, (await Invoke(null, OptionsFor(), path: path, method: method)).StatusCode);

    [Fact]
    public async Task Exp_still_applies_to_old_signature_during_migration()
    {
        var options = OptionsFor();
        options.AcceptLegacyHashedKey = true;
        options.JwtLegacyHashedKeyUntilUtc = "2026-09-09T00:00:00Z";
        var token = Sign(new { senha = Password, datahora = "2026-09-08T12:00:00Z", exp = Now.ToUnixTimeSeconds() });
        var parts = token.Split('.');
        var signature = HMACSHA256.HashData(SHA256.HashData(Encoding.UTF8.GetBytes("curta")), Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"));
        var response = await Invoke($"{parts[0]}.{parts[1]}.{Base64Url(signature)}", options);
        Assert.Equal(401, response.StatusCode);
        response.Body.Position = 0;
        Assert.Equal("O JWT está expirado", (await JsonNode.ParseAsync(response.Body))!["message"]!.GetValue<string>());
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Sign(object payload) => ApiJwtKeyHelper.CreateToken(payload, "curta");
    private static ApiPartnerOptions OptionsFor(string secret = "curta") => new()
    {
        JwtSecret = secret, JwtPassword = Password, JwtDatetimeToleranceHours = 4
    };
    private static ApiPublicaService Service(ApiPartnerOptions options) =>
        new(new ApiV1NoDatabase(), Options.Create(options), null!, new ApiV1FixedTimeProvider(Now));

    private static async Task<HttpResponse> Invoke(string? authorization, ApiPartnerOptions options,
        DateTimeOffset? now = null, string path = "/apiconteudos/v1/variaveis", string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        if (authorization is not null) context.Request.Headers.Authorization = authorization;
        var middleware = new ApiPartnerJwtMiddleware(_ => Task.CompletedTask, Options.Create(options), new ApiV1FixedTimeProvider(now ?? Now));
        await middleware.InvokeAsync(context);
        return context.Response;
    }
}
