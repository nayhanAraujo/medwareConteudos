using Microsoft.Extensions.Configuration;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;

namespace ConversorHtml.Tests;

public class ProductionSecurityTests
{
    private static readonly string Temp = Path.GetTempPath();
    private static IConfiguration Config(string? key = null, string? value = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["DataPaths:Root"] = Temp, ["AllowedHosts"] = "conteudos.example.test;127.0.0.1",
            ["Documentation:PortalUrl"] = "https://conteudos.example.test/apiconteudos/docs",
            ["Security:TrustedProxies:0"] = "127.0.0.1"
        };
        if (key is not null) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
    private static void Validate(IConfiguration config, WebAuthOptions? web = null, ApiPartnerOptions? partner = null) =>
        ProductionSecurity.ValidateConfiguration(config,
            web ?? new() { JwtSecret = "fixture-web-secret-only-not-for-deployment-123" },
            partner ?? new() { JwtSecret = "fixture-partner-secret", JwtPassword = "fixture-password" },
            new() { Database = Path.Combine(Temp, "REFERENCIAS.FDB"), Password = "fixture-db-password" },
            new() { Database = Path.Combine(Temp, "ASSISTENTE.FDB"), Password = "fixture-db-password" });

    [Fact] public void Valid_explicit_production_configuration_is_accepted() => Validate(Config());
    [Theory]
    [InlineData("AllowedHosts", "*")]
    [InlineData("DataPaths:Root", "relative")]
    [InlineData("Documentation:PortalUrl", "http://localhost:3000")]
    [InlineData("Documentation:AllowWrites", "true")]
    [InlineData("Security:CorsOrigins:0", "*")]
    [InlineData("Security:CorsOrigins:0", "https://host.test/path")]
    public void Unsafe_configuration_fails_before_database_use(string key, string value) =>
        Assert.Throws<InvalidOperationException>(() => Validate(Config(key, value)));
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("mdw-web-dev-secret-change-me-2026-local-migration-only")]
    public void Unsafe_web_secret_is_rejected(string secret) =>
        Assert.Throws<InvalidOperationException>(() => Validate(Config(), new() { JwtSecret = secret }));
    [Fact] public void Development_bypass_cannot_be_enabled_in_production() =>
        Assert.Throws<InvalidOperationException>(() => Validate(Config(), new() { JwtSecret = new string('x', 40), DevUserEnabled = true }));
    [Fact] public void Known_partner_password_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() => Validate(Config(), partner: new() { JwtSecret = "own-partner-key", JwtPassword = "Medware!111096" }));
    [Fact] public void Data_root_does_not_require_a_source_checkout() =>
        Assert.Equal(Path.GetFullPath(Temp), MigrationRootResolver.ResolveMigrationRoot(Config(), AppContext.BaseDirectory));
}
