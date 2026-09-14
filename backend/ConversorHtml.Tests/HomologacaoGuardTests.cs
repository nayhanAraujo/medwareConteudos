using System.Collections;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Assistente.Publicacao;

namespace ConversorHtml.Tests;

// Loader/Configure sanitize process environment; never run these alongside other collections.
[CollectionDefinition("Homologacao isolation", DisableParallelization = true)]
public sealed class HomologacaoIsolationCollection;

[Collection("Homologacao isolation")]
public sealed class HomologacaoGuardTests
{
    [Fact]
    public void Validate_AceitaPreparacaoIsoladaSemAbrirBanco()
    {
        using var fixture = new Sandbox();
        fixture.Validate();
        Assert.Equal(fixture.Files, MigrationRootResolver.ResolveMigrationRoot(fixture.Config, fixture.Environment.ContentRootPath));
        Assert.Null(MigrationRootResolver.ResolveLegacyRoot(fixture.Config, fixture.Files, fixture.Environment.ContentRootPath));
    }

    [Theory]
    [InlineData("Homologacao:Enabled", "false")]
    [InlineData("Homologacao:Configured", "false")]
    [InlineData("Homologacao:IsolatedEnvironment", "false")]
    [InlineData("Homologacao:Root", "relative")]
    [InlineData("MigrationPaths:Root", "relative")]
    [InlineData("PublicacaoAssistente:Enabled", "true")]
    [InlineData("PublicacaoAssistente:Enabled", "invalid")]
    [InlineData("PublicacaoAssistente:Enabled", null)]
    [InlineData("Documentation:InstanceId", "")]
    [InlineData("Documentation:InstanceId", "00000000000000000000000000000000")]
    [InlineData("Documentation:AllowWrites", "invalid")]
    [InlineData("Conversion:Provider", "Cursor")]
    [InlineData("Voice:Provider", "Cursor")]
    [InlineData("Voice:TranscriptionProvider", "Whisper")]
    [InlineData("Cursor:ApiKey", "synthetic-external-secret")]
    [InlineData("AzureOpenAI:Endpoint", "https://invalid.example")]
    [InlineData("Urls", "http://0.0.0.0:5081")]
    [InlineData("Urls", "http://127.0.0.1:5081;http://0.0.0.0:5081")]
    [InlineData("Kestrel:Endpoints:Public:Url", "http://0.0.0.0:5081")]
    public void Validate_RecusaConfiguracaoInvalida(string key, string? value)
    {
        using var fixture = new Sandbox();
        fixture.Config[key] = value;
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
    }

    [Fact]
    public void Validate_RecusaOriginaisEmAmbosOsCaminhosReferencias()
    {
        using var fixture = new Sandbox();
        fixture.Firebird.Database = fixture.OriginalReferences;
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
        fixture.Firebird.Database = fixture.References;
        fixture.Config["Firebird:Database"] = fixture.OriginalReferences;
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
    }

    [Fact]
    public void Validate_RecusaOriginalAssistenteEConnectionStringAlternativa()
    {
        using var fixture = new Sandbox();
        fixture.Assistant.Database = fixture.OriginalAssistant;
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
        fixture.Assistant.Database = fixture.AssistantCopy;
        fixture.Config["ConnectionStrings:FirebirdDb"] = "User=test;Password=test;DataSource=127.0.0.1;Database=" + fixture.OriginalReferences;
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
    }

    [Fact]
    public void Validate_RecusaServidorRemotoMesmoComCaminhoDeCopia()
    {
        using var fixture = new Sandbox();
        fixture.Firebird.Host = "remote.invalid";
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
    }

    [Fact]
    public void Validate_RecusaRaizesOriginaisEColisaoDePrefixo()
    {
        using var fixture = new Sandbox();
        fixture.Config["MigrationPaths:Root"] = fixture.Workspace;
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
        fixture.Config["MigrationPaths:Root"] = fixture.Files + "-escape";
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
        fixture.Config["MigrationPaths:Root"] = fixture.Files;
        fixture.Config["LegacyPaths:RepoRoot"] = fixture.Workspace;
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
    }

    [Fact]
    public void ResolveFilesRoot_FalhaSemFallbackQuandoRaizEstaAusente()
    {
        using var fixture = new Sandbox();
        fixture.Config["MigrationPaths:Root"] = "";
        Assert.Throws<HomologacaoValidationException>(() => MigrationRootResolver.ResolveMigrationRoot(fixture.Config));
    }

    [Theory]
    [InlineData("FIREBIRD_DB")]
    [InlineData("ASSISTENTE_FIREBIRD_DATABASE")]
    [InlineData("API_JWT_SECRET")]
    public void Validate_RecusaOverridePerigosoPosteriorAoConfigure(string key)
    {
        using var fixture = new Sandbox();
        Assert.Throws<HomologacaoValidationException>(() => fixture.ValidateEnvironment(name => name == key ? "synthetic" : null));
    }

    [Fact]
    public void Validate_RecusaPublicacaoEfetivaMesmoComJsonDesabilitado()
    {
        using var fixture = new Sandbox();
        fixture.Publication.Enabled = true;
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
    }

    [Fact]
    public void Validate_RecusaSegredosPadraoReutilizadosOuDivergentes()
    {
        using var fixture = new Sandbox();
        var original = fixture.WebAuth.JwtSecret;
        fixture.WebAuth.JwtSecret = "mdw-web-dev-secret-change-me-2026-local-migration-only";
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
        fixture.WebAuth.JwtSecret = original;
        fixture.Partner.JwtSecret = original;
        fixture.Config["ApiPartner:JwtSecret"] = original;
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
    }

    [Fact]
    public void Validate_RecusaManifestoAusenteCorrompidoOuDeOutraInstancia()
    {
        using var fixture = new Sandbox();
        File.Delete(fixture.Manifest);
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
        File.WriteAllText(fixture.Manifest, "invalid");
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
        fixture.WriteManifest();
        fixture.Config["Documentation:InstanceId"] = Guid.NewGuid().ToString("N");
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
    }

    [Fact]
    public void Validate_RecusaArquivoAusente()
    {
        using var fixture = new Sandbox();
        File.Delete(fixture.AssistantCopy);
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
    }

    [Fact]
    public void Validate_RecusaLinkDeUploadParaOriginal()
    {
        using var fixture = new Sandbox();
        var link = Path.Combine(fixture.Files, "uploads", "escape");
        // Junction on Windows does not require symlink privilege; Unix uses a directory symlink.
        if (OperatingSystem.IsWindows())
        {
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            // Fixed verb and individually quoted, freshly generated test-only paths; no delete/move via cmd.
            start.Arguments = $"/d /c mklink /J \"{link}\" \"{fixture.Workspace}\"";
            using var process = System.Diagnostics.Process.Start(start)!;
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, fixture.Workspace);
        try { Assert.Throws<HomologacaoValidationException>(fixture.Validate); }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void Loader_PulaEnvAncestralEIgnoraAmbienteHerdado()
    {
        using var fixture = new Sandbox();
        using var variables = new RestoreDangerousEnvironment();
        File.WriteAllText(Path.Combine(fixture.Workspace, ".env"), "FIREBIRD_DB=original\nHOMOLOGACAO_TEST_ENV_SENTINEL=loaded");
        System.Environment.SetEnvironmentVariable("FIREBIRD_DB", fixture.OriginalReferences);
        System.Environment.SetEnvironmentVariable("Firebird__Database", fixture.OriginalReferences);
        System.Environment.SetEnvironmentVariable("ASSISTENTE_FIREBIRD_DATABASE", fixture.OriginalAssistant);
        var builder = new ConfigurationBuilder().AddInMemoryCollection(fixture.Config.AsEnumerable()).AddEnvironmentVariables();
        EnvFileLoader.LoadFromRepoRoot(builder, fixture.Environment.ContentRootPath, "Homologacao");
        var config = builder.Build();
        Assert.Equal(fixture.References, config["Firebird:Database"]);
        Assert.Null(System.Environment.GetEnvironmentVariable("FIREBIRD_DB"));
        Assert.Null(System.Environment.GetEnvironmentVariable("ASSISTENTE_FIREBIRD_DATABASE"));
        Assert.Null(System.Environment.GetEnvironmentVariable("HOMOLOGACAO_TEST_ENV_SENTINEL"));
        Assert.DoesNotContain(fixture.OriginalReferences, EnvFileLoader.GetFirebirdConnectionString(config));
    }

    [Fact]
    public void Configure_CongelaConfiguracaoSemHotReload()
    {
        using var fixture = new Sandbox();
        using var variables = new RestoreDangerousEnvironment();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Homologacao", ContentRootPath = fixture.Environment.ContentRootPath
        });
        builder.Configuration.AddConfiguration(fixture.Config);
        HomologacaoGuard.Configure(builder);
        fixture.Config["Firebird:Database"] = fixture.OriginalReferences;
        Assert.Equal(fixture.References, builder.Configuration["Firebird:Database"]);
        Assert.Single(builder.Configuration.Sources);
    }

    [Fact]
    public void Validate_ForaDoPerfilNaoAtivaSandboxImplicitamente()
    {
        using var fixture = new Sandbox();
        fixture.Environment.EnvironmentName = "Development";
        Assert.Throws<HomologacaoValidationException>(fixture.Validate);
        fixture.Config["Homologacao:Enabled"] = "false";
        fixture.Config["Homologacao:IsolatedEnvironment"] = "false";
        fixture.Validate();
    }

    [Theory]
    [InlineData("FIREBIRD_PASSWORD", true)]
    [InlineData("WebAuth__JwtSecret", true)]
    [InlineData("ConnectionStrings__FirebirdDb", true)]
    [InlineData("OPENAI_API_KEY", true)]
    [InlineData("ASPNETCORE_URLS", true)]
    [InlineData("NUXT_DOCS_SANDBOX_INSTANCE_ID", false)]
    [InlineData("SystemRoot", false)]
    public void EnvironmentPolicy_ClassificaOverrides(string name, bool expected)
        => Assert.Equal(expected, HomologacaoGuard.IsDangerousEnvironmentVariable(name));

    private sealed class RestoreDangerousEnvironment : IDisposable
    {
        private readonly Dictionary<string, string> _original = System.Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .Where(entry => HomologacaoGuard.IsDangerousEnvironmentVariable((string)entry.Key))
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);

        public void Dispose()
        {
            foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
                if (HomologacaoGuard.IsDangerousEnvironmentVariable((string)entry.Key))
                    System.Environment.SetEnvironmentVariable((string)entry.Key, null);
            foreach (var pair in _original) System.Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    private sealed class Sandbox : IDisposable
    {
        public string Workspace { get; } = Path.Combine(Path.GetTempPath(), "mdw-homologacao-tests-" + Guid.NewGuid().ToString("N"));
        public string Root { get; }
        public string Files => Path.Combine(Root, "files");
        public string References => Path.Combine(Root, "databases", "REFERENCIAS.FDB");
        public string AssistantCopy => Path.Combine(Root, "databases", "ASSISTENTE.FDB");
        public string OriginalReferences => Path.Combine(Workspace, "bd", "REFERENCIAS.FDB");
        public string OriginalAssistant => Path.Combine(Workspace, "bd", "ASSISTENTE.FDB");
        public string Manifest => Path.Combine(Root, "manifest.json");
        public IConfigurationRoot Config { get; }
        public TestEnvironment Environment { get; }
        public FirebirdOptions Firebird { get; }
        public AssistantFirebirdOptions Assistant { get; }
        public WebAuthOptions WebAuth { get; }
        public ApiPartnerOptions Partner { get; }
        public PublicacaoOptions Publication { get; } = new() { Enabled = false };

        public Sandbox()
        {
            Root = Path.Combine(Workspace, ".homologacao", "20260908-120000-000-" + Guid.NewGuid().ToString("N")[..8]);
            Environment = new TestEnvironment { ContentRootPath = Path.Combine(Workspace, "backend", "MdwConteudos.Api") };
            foreach (var directory in new[] { Environment.ContentRootPath, Path.Combine(Workspace, "frontend", "nuxt-app"),
                         Path.Combine(Root, "databases"), Path.Combine(Files, "static", "uploads"), Path.Combine(Files, "uploads") })
                Directory.CreateDirectory(directory);
            // Synthetic bytes, no Firebird server or connection is required by guard tests.
            File.WriteAllBytes(References, [1]);
            File.WriteAllBytes(AssistantCopy, [1]);
            Firebird = new() { Host = "127.0.0.1", Port = 3052, Database = References, User = "test", Password = "synthetic-local" };
            Assistant = new() { Host = "127.0.0.1", Port = 3050, Database = AssistantCopy, User = "test", Password = "synthetic-local" };
            WebAuth = new() { JwtSecret = Secret(), Issuer = "MdwConteudos.Homologacao", Audience = "MdwConteudos.Homologacao.Web" };
            Partner = new() { JwtSecret = Secret(), JwtPassword = Secret() };
            Config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Homologacao:Enabled"] = "True", ["Homologacao:Configured"] = "true", ["Homologacao:IsolatedEnvironment"] = "true",
                ["Homologacao:WorkspaceRoot"] = Workspace, ["Homologacao:Root"] = Root,
                ["Homologacao:OriginalReferencias"] = OriginalReferences, ["Homologacao:OriginalAssistente"] = OriginalAssistant,
                ["MigrationPaths:Root"] = Files, ["Firebird:Database"] = References, ["Firebird:Host"] = Firebird.Host,
                ["Firebird:Port"] = "3052", ["Firebird:User"] = Firebird.User, ["Firebird:Password"] = Firebird.Password,
                ["Documentation:AllowWrites"] = "true", ["Documentation:InstanceId"] = Guid.NewGuid().ToString("N"),
                ["PublicacaoAssistente:Enabled"] = "False", ["WebAuth:JwtSecret"] = WebAuth.JwtSecret,
                ["ApiPartner:JwtSecret"] = Partner.JwtSecret, ["ApiPartner:JwtPassword"] = Partner.JwtPassword,
                ["Conversion:Provider"] = "Mock", ["Voice:Provider"] = "Mock", ["Voice:TranscriptionProvider"] = "Browser",
                ["Urls"] = "http://127.0.0.1:5081"
            }).Build();
            WriteManifest();
        }

        public void WriteManifest() => File.WriteAllText(Manifest, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, prepared = true, instanceId = Config["Documentation:InstanceId"], root = Root,
            referencias = References, assistente = AssistantCopy, originalReferencias = OriginalReferences, originalAssistente = OriginalAssistant
        }));

        public void Validate() => ValidateEnvironment(_ => null);
        public void ValidateEnvironment(Func<string, string?> readEnvironment)
            => HomologacaoGuard.Validate(Config, Environment, Firebird, Assistant, WebAuth, Partner, Publication, readEnvironment);
        private static string Secret() => "homol-" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        public void Dispose()
        {
            var full = Path.GetFullPath(Workspace);
            if (Path.GetDirectoryName(full) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
                || !Path.GetFileName(full).StartsWith("mdw-homologacao-tests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Cleanup fora do diretório temporário de teste recusado.");
            Directory.Delete(full, recursive: true);
            (Config as IDisposable)?.Dispose();
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Homologacao";
        public string ApplicationName { get; set; } = "MdwConteudos.Api";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
