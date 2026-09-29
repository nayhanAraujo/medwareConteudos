using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Modules.Assistente.Publicacao;

namespace MdwConteudos.Api.Infrastructure;

/// <summary>Fail-closed local sandbox. Does not open connections or change database schemas.</summary>
public static class HomologacaoGuard
{
    public const string EnvironmentName = "Homologacao";
    private const string ConfiguredKey = "Homologacao:Configured";
    private static readonly string[] DangerousPrefixes =
    [
        "FIREBIRD", "ASSISTENTE_FIREBIRD", "ASSISTANTFIREBIRD", "LOCAL_DB_", "ISC_",
        "API_JWT_", "APIPARTNER", "WEBAUTH", "CONNECTIONSTRINGS", "LEGACYPATHS", "MIGRATIONPATHS",
        "HOMOLOGACAO", "DOCUMENTATION", "PUBLICACAO", "CURSOR", "OPENAI", "AZUREOPENAI", "AZURE_OPENAI",
        "VOICE", "CONVERSION", "KESTREL", "ASPNETCORE_URLS", "DOTNET_URLS", "ASPNETCORE_HTTP_PORTS",
        "ASPNETCORE_HTTPS_PORTS", "ASPNETCORE_HOSTINGSTARTUP", "DOTNET_STARTUP_HOOKS"
    ];

    public static bool IsDangerousEnvironmentVariable(string name)
        => DangerousPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public static bool IsEnabled(IConfiguration config)
        => string.Equals(config["Homologacao:Enabled"], "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(config["Homologacao:IsolatedEnvironment"], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Call after the local JSON override, before registering options/authentication/services.</summary>
    public static void Configure(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsEnvironment(EnvironmentName))
        {
            Require(!IsEnabled(builder.Configuration), "perfil diferente de Homologacao com configuração de sandbox");
            return;
        }

        EnvFileLoader.LoadFromRepoRoot(builder.Configuration, builder.Environment.ContentRootPath, EnvironmentName);
        Require(IsBoolean(builder.Configuration, "Homologacao:Enabled", true), "Homologacao:Enabled deve ser true");
        // Freeze the final values: a JSON hot reload must never switch paths/secrets after validation.
        var values = builder.Configuration.AsEnumerable().ToDictionary(pair => pair.Key, pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        values[ConfiguredKey] = "true";
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(values);
    }

    /// <summary>Call immediately after Build(), BEFORE EnsureSchemaAsync, middleware or hosted services start.</summary>
    public static void Validate(WebApplication app)
    {
        if (!app.Environment.IsEnvironment(EnvironmentName))
        {
            Require(!IsEnabled(app.Configuration), "perfil diferente de Homologacao com configuração de sandbox");
            return;
        }

        try
        {
            Validate(app.Configuration, app.Environment,
                app.Services.GetRequiredService<IOptions<FirebirdOptions>>().Value,
                app.Services.GetRequiredService<IOptions<AssistantFirebirdOptions>>().Value,
                app.Services.GetRequiredService<IOptions<WebAuthOptions>>().Value,
                app.Services.GetRequiredService<IOptions<ApiPartnerOptions>>().Value,
                app.Services.GetRequiredService<IOptions<PublicacaoOptions>>().Value);
            Require(app.Services.GetRequiredService<IFirebirdConnectionFactory>().GetType() == typeof(FirebirdConnectionFactory),
                "fábrica REFERENCIAS não reconhecida");
            Require(app.Services.GetRequiredService<IAssistantFirebirdConnectionFactory>().GetType() == typeof(AssistantFirebirdConnectionFactory),
                "fábrica ASSISTENTE não reconhecida");
        }
        catch (Exception exception) when (exception is not HomologacaoValidationException)
        {
            // Option binding / connection-string parsing can contain raw credentials in exceptions.
            throw new HomologacaoValidationException("Homologacao: configuração efetiva inválida; nenhum banco foi aberto pelo guard.");
        }
    }

    public static void Validate(IConfiguration config, IHostEnvironment environment, FirebirdOptions firebird,
        AssistantFirebirdOptions assistant, WebAuthOptions webAuth, ApiPartnerOptions partner,
        PublicacaoOptions publication, Func<string, string?>? readEnvironment = null)
    {
        if (!environment.IsEnvironment(EnvironmentName))
        {
            Require(!IsEnabled(config), "perfil diferente de Homologacao com configuração de sandbox");
            return;
        }
        Require(config[ConfiguredKey] == "true" && config["Homologacao:IsolatedEnvironment"] == "true",
            "Configure deve executar antes de registrar serviços");
        Require(IsBoolean(config, "Homologacao:Enabled", true), "Homologacao:Enabled deve ser true");
        readEnvironment ??= Environment.GetEnvironmentVariable;
        foreach (var key in new[] { "FIREBIRD_DB", "FIREBIRD_HOST", "FIREBIRD_PORT", "FIREBIRD_PASSWORD", "LOCAL_DB_PASSWORD",
                     "ASSISTENTE_FIREBIRD_DATABASE", "ASSISTENTE_FIREBIRD_HOST", "ASSISTENTE_FIREBIRD_PORT",
                     "ASSISTENTE_FIREBIRD_PASSWORD", "ASSISTENTE_FIREBIRD_CLIENT_LIBRARY", "API_JWT_SECRET", "API_JWT_PASSWORD" })
            Require(string.IsNullOrEmpty(readEnvironment(key)), "override de ambiente perigoso após Configure");

        var workspace = AbsolutePath(config["Homologacao:WorkspaceRoot"]);
        Require(SamePath(environment.ContentRootPath, Path.Combine(workspace, "backend", "MdwConteudos.Api")),
            "WorkspaceRoot não corresponde ao ContentRoot da API");
        Require(Directory.Exists(Path.Combine(workspace, "frontend", "nuxt-app")), "workspace inválido");
        var root = AbsolutePath(config["Homologacao:Root"]);
        Require(SamePath(Path.GetDirectoryName(root)!, Path.Combine(workspace, ".homologacao"))
                && Regex.IsMatch(Path.GetFileName(root), @"^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}$"), "diretório por execução inválido");
        ValidateExistingPath(root, directory: true);
        var files = ResolveFilesRoot(config);
        ValidateExistingTree(files);
        ValidateExistingPath(Path.Combine(files, "static", "uploads"), directory: true);
        ValidateExistingPath(Path.Combine(files, "uploads"), directory: true);
        Require(string.IsNullOrWhiteSpace(config["LegacyPaths:RepoRoot"]), "fallback LegacyPaths não permitido");
        Require(MigrationRootResolver.ResolveLegacyRoot(config, files, environment.ContentRootPath) is null,
            "fallback de arquivos legado não permitido");

        var referencias = Path.Combine(root, "databases", "REFERENCIAS.FDB");
        var assistente = Path.Combine(root, "databases", "ASSISTENTE.FDB");
        foreach (var database in new[] { referencias, assistente }) ValidateExistingPath(database, directory: false);
        foreach (var key in new[] { "OriginalReferencias", "OriginalAssistente" })
        {
            var original = AbsolutePath(config["Homologacao:" + key]);
            Require(!IsWithin(original, Path.Combine(workspace, ".homologacao")), "origem não pode ser outra homologação");
            Require(!SamePath(original, referencias) && !SamePath(original, assistente), "conexão aponta para original");
        }

        ValidateConnection(firebird.BuildConnectionString("UTF8"), referencias, "REFERENCIAS/fábrica");
        ValidateConnection(firebird.BuildConnectionString("ISO8859_1"), referencias, "REFERENCIAS/fallback");
        ValidateConnection(EnvFileLoader.GetFirebirdConnectionString(config), referencias, "REFERENCIAS/legado");
        // Assistant factory applies these overrides again in its constructor.
        var effectiveAssistant = new AssistantFirebirdOptions
        {
            Host = assistant.Host, Port = assistant.Port, Database = assistant.Database,
            User = assistant.User, Password = assistant.Password, Charset = assistant.Charset,
            ClientLibrary = assistant.ClientLibrary
        };
        effectiveAssistant.ApplyEnvironmentVariables(readEnvironment);
        ValidateConnection(effectiveAssistant.BuildConnectionString(environment.ContentRootPath), assistente, "ASSISTENTE/fábrica");
        if (!string.IsNullOrWhiteSpace(config.GetConnectionString("FirebirdDb")))
            ValidateConnection(config.GetConnectionString("FirebirdDb")!, referencias, "FirebirdDb");

        Require(IsBoolean(config, "PublicacaoAssistente:Enabled", false) && !publication.Enabled, "publicação deve estar explicitamente desabilitada");
        Require(bool.TryParse(config["Documentation:AllowWrites"], out _), "Documentation:AllowWrites inválido");
        Require(Guid.TryParseExact(config["Documentation:InstanceId"], "N", out var instance) && instance != Guid.Empty,
            "Documentation:InstanceId inválido");
        ValidateManifest(config, root, referencias, assistente);
        Require(IsTestSecret(webAuth.JwtSecret) && IsTestSecret(partner.JwtSecret) && IsTestSecret(partner.JwtPassword),
            "segredos de teste devem ser gerados pela preparação");
        Require(webAuth.JwtSecret == config["WebAuth:JwtSecret"] && partner.JwtSecret == config["ApiPartner:JwtSecret"]
                && partner.JwtPassword == config["ApiPartner:JwtPassword"], "segredos efetivos divergem da configuração isolada");
        Require(new[] { webAuth.JwtSecret, partner.JwtSecret, partner.JwtPassword }.Distinct().Count() == 3,
            "segredos devem ser independentes");
        Require(!webAuth.DevUserEnabled || (webAuth.DevUsername == "homologacao" && IsTestSecret(webAuth.DevPassword)),
            "usuário de teste inválido");
        Require(webAuth.Issuer == "MdwConteudos.Homologacao" && webAuth.Audience == "MdwConteudos.Homologacao.Web",
            "issuer/audience devem ser exclusivos de homologação");
        Require(config["Conversion:Provider"] == "Mock" && config["Voice:Provider"] == "Mock"
                && config["Voice:TranscriptionProvider"] == "Browser", "provedores externos não permitidos");
        foreach (var key in new[] { "Cursor:ApiKey", "AzureOpenAI:ApiKey", "Voice:OpenAiApiKey", "AzureOpenAI:Endpoint" })
            Require(string.IsNullOrEmpty(config[key]), "credencial ou endpoint externo configurado");
        var urls = (config["Urls"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        Require(urls.Length == 1 && Uri.TryCreate(urls[0], UriKind.Absolute, out var uri)
                && uri.Scheme == "http" && IsLoopback(uri.Host) && uri.Port > 0 && uri.AbsolutePath == "/",
            "Urls deve conter um único listener HTTP loopback");
        Require(!config.GetSection("Kestrel:Endpoints").GetChildren().Any(), "endpoints Kestrel adicionais não permitidos");
    }

    public static string ResolveFilesRoot(IConfiguration config)
    {
        var root = AbsolutePath(config["Homologacao:Root"]);
        var files = AbsolutePath(config["MigrationPaths:Root"]);
        Require(SamePath(files, Path.Combine(root, "files")), "MigrationPaths:Root deve apontar para files da execução");
        ValidateExistingPath(files, directory: true);
        return files;
    }

    /// <summary>Map persisted legacy paths to copied assets only; never fall back to the original file.</summary>
    public static string? ResolveCopiedFile(string filesRoot, string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        var value = stored.Trim().Replace('\\', '/');
        if (value.StartsWith("//") || value.Contains("://") || value.Split('/').Contains("..")) return null;
        var root = Path.GetFullPath(filesRoot);
        if (Path.IsPathFullyQualified(value) && IsWithin(value, root))
            return CheckedCopiedFile(Path.GetFullPath(value), root);
        var marker = value.IndexOf("/static/", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0) value = value[(marker + 1)..];
        else
        {
            marker = value.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0) value = value[(marker + 1)..];
            else if (Path.IsPathFullyQualified(value) || value.Contains(':')) return null;
        }
        value = value.TrimStart('/');
        foreach (var candidate in new[] { Path.Combine(root, value), Path.Combine(root, "static", value), Path.Combine(root, "static", "uploads", value) })
        {
            var found = CheckedCopiedFile(Path.GetFullPath(candidate), root);
            if (found is not null) return found;
        }
        return null;
    }

    private static string? CheckedCopiedFile(string path, string root)
    {
        if (!IsWithin(path, root) || !File.Exists(path)) return null;
        ValidateExistingPath(path, directory: false);
        return path;
    }

    private static void ValidateConnection(string connectionString, string expectedDatabase, string label)
    {
        try
        {
            var parsed = new FbConnectionStringBuilder(connectionString);
            var database = parsed.Database;
            // FirebirdOptions usa a sintaxe Database=host/porta:caminho; o legado usa DataSource/Port.
            var embedded = Regex.Match(database, @"^(localhost|127\.0\.0\.1)/(\d+):(.+)$", RegexOptions.IgnoreCase);
            if (embedded.Success)
            {
                Require(int.TryParse(embedded.Groups[2].Value, out var port) && port is >= 1 and <= 65535, "porta Firebird inválida");
                database = embedded.Groups[3].Value;
            }
            Require((IsLoopback(parsed.DataSource) || (embedded.Success && string.IsNullOrEmpty(parsed.DataSource)))
                    && parsed.Port is >= 1 and <= 65535
                    && parsed.ServerType == FbServerType.Default, "servidor Firebird deve ser local TCP");
            Require(SamePath(AbsolutePath(database), expectedDatabase), label + " não aponta para a cópia esperada");
            Require(!string.IsNullOrWhiteSpace(parsed.UserID) && !string.IsNullOrWhiteSpace(parsed.Password), "credenciais Firebird ausentes");
        }
        catch (Exception exception) when (exception is not HomologacaoValidationException)
        {
            throw new HomologacaoValidationException("Homologacao: conexão inválida (" + label + "); valores omitidos.");
        }
    }

    private static void ValidateManifest(IConfiguration config, string root, string referencias, string assistente)
    {
        try
        {
            var path = Path.Combine(root, "manifest.json");
            ValidateExistingPath(path, directory: false);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var manifest = document.RootElement;
            Require(manifest.GetProperty("schemaVersion").GetInt32() == 1
                    && manifest.GetProperty("prepared").GetBoolean()
                    && manifest.GetProperty("instanceId").GetString() == config["Documentation:InstanceId"]
                    && SamePath(manifest.GetProperty("root").GetString()!, root)
                    && SamePath(manifest.GetProperty("referencias").GetString()!, referencias)
                    && SamePath(manifest.GetProperty("assistente").GetString()!, assistente)
                    && SamePath(manifest.GetProperty("originalReferencias").GetString()!, config["Homologacao:OriginalReferencias"]!)
                    && SamePath(manifest.GetProperty("originalAssistente").GetString()!, config["Homologacao:OriginalAssistente"]!),
                "manifesto não corresponde à preparação concluída");
        }
        catch (Exception exception) when (exception is not HomologacaoValidationException)
        {
            throw new HomologacaoValidationException("Homologacao: manifesto ausente ou inválido; execute Prepare-Homologacao.ps1.");
        }
    }

    private static bool IsTestSecret(string value)
    {
        if (!value.StartsWith("homol-", StringComparison.Ordinal)) return false;
        try { return Convert.FromBase64String(value[6..]).Length >= 32; }
        catch (FormatException) { return false; }
    }

    private static string AbsolutePath(string? path)
    {
        Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path)
                && !path.StartsWith(@"\\") && !path.StartsWith("//"), "caminho deve ser absoluto e local");
        var full = Path.GetFullPath(path!);
        Require(!SamePath(full, Path.GetPathRoot(full)!), "raiz de volume não permitida");
        if (OperatingSystem.IsWindows())
            Require(!full[2..].Contains(':') && !full.Contains('~'), "caminho alternativo/ADS não permitido");
        return Path.TrimEndingDirectorySeparator(full);
    }

    private static void ValidateExistingTree(string root)
    {
        ValidateExistingPath(root, directory: true);
        foreach (var path in Directory.EnumerateFileSystemEntries(root))
        {
            var isDirectory = Directory.Exists(path);
            ValidateExistingPath(path, isDirectory);
            if (isDirectory) ValidateExistingTree(path);
        }
    }

    private static void ValidateExistingPath(string path, bool directory)
    {
        Require(directory ? Directory.Exists(path) : File.Exists(path), "artefato isolado ausente");
        for (var item = Path.GetFullPath(path); item is not null; item = Path.GetDirectoryName(item))
            Require((File.GetAttributes(item) & FileAttributes.ReparsePoint) == 0, "symlink/junction não permitido");
        if (!directory && OperatingSystem.IsWindows())
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Require(GetFileInformationByHandle(handle, out var info) && info.NumberOfLinks == 1, "hardlink não permitido");
        }
    }

    private static bool SamePath(string left, string right)
        => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsWithin(string path, string parent)
        => SamePath(path, parent) || Path.GetFullPath(path).StartsWith(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsLoopback(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1";

    private static bool IsBoolean(IConfiguration config, string key, bool expected)
        => bool.TryParse(config[key], out var value) && value == expected;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new HomologacaoValidationException("Homologacao: " + message + ".");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, AccessTime, WriteTime;
        public uint VolumeSerialNumber, SizeHigh, SizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
}

public sealed class HomologacaoValidationException(string message) : InvalidOperationException(message);
