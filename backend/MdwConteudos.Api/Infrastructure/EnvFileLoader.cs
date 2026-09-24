namespace MdwConteudos.Api.Infrastructure;

public static class EnvFileLoader
{
    public static void LoadFromRepoRoot(IConfigurationBuilder config, string contentRoot, string? environmentName = null)
    {
        environmentName ??= Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            config.AddEnvironmentVariables();
            return;
        }
        if (string.Equals(environmentName, HomologacaoGuard.EnvironmentName, StringComparison.OrdinalIgnoreCase))
        {
            // CreateBuilder já adicionou providers de ambiente. Removê-los é necessário:
            // simplesmente deixar de chamar AddEnvironmentVariables não basta.
            foreach (var source in config.Sources.OfType<Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationSource>().ToArray())
                config.Sources.Remove(source);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                if (HomologacaoGuard.IsDangerousEnvironmentVariable((string)entry.Key))
                    Environment.SetEnvironmentVariable((string)entry.Key, null);
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Homologacao:IsolatedEnvironment"] = "true" });
            return; // Não procura nem importa .env, inclusive os ancestrais.
        }

        var dir = contentRoot;
        for (var i = 0; i < 6; i++)
        {
            var envPath = Path.Combine(dir, ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t.StartsWith('#')) continue;
                    var eq = t.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = t[..eq].Trim();
                    var val = t[(eq + 1)..].Trim().Trim('"');
                    Environment.SetEnvironmentVariable(key, val);
                }
                break;
            }
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        config.AddEnvironmentVariables();
    }

    public static string GetFirebirdConnectionString(IConfiguration config)
    {
        if (HomologacaoGuard.IsEnabled(config))
        {
            // Nenhum fallback para ambiente, senha padrão ou BD original neste perfil.
            if (!int.TryParse(config["Firebird:Port"], out var homolPort) || homolPort is < 1 or > 65535)
                throw new InvalidOperationException("Homologacao: Firebird:Port inválida.");
            return new FirebirdSql.Data.FirebirdClient.FbConnectionStringBuilder
            {
                DataSource = config["Firebird:Host"] ?? "",
                Port = homolPort,
                Database = config["Firebird:Database"] ?? "",
                UserID = config["Firebird:User"] ?? "",
                Password = config["Firebird:Password"] ?? "",
                Charset = config["Firebird:Charset"] ?? "UTF8"
            }.ToString();
        }

        var host = Environment.GetEnvironmentVariable("FIREBIRD_HOST") ?? config["Firebird:Host"] ?? "127.0.0.1";
        var port = Environment.GetEnvironmentVariable("FIREBIRD_PORT") ?? config["Firebird:Port"] ?? "3052";
        var db = Environment.GetEnvironmentVariable("FIREBIRD_DB")
                 ?? config["Firebird:Database"]
                 ?? "";
        if (string.IsNullOrWhiteSpace(db))
        {
            var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
            if (repoRoot != null)
                db = Path.Combine(repoRoot, "BD", "REFERENCIAS.FDB");
        }
        var user = Environment.GetEnvironmentVariable("FIREBIRD_USER") ?? config["Firebird:User"] ?? "SYSDBA";
        var pass = Environment.GetEnvironmentVariable("FIREBIRD_PASSWORD")
                   ?? Environment.GetEnvironmentVariable("LOCAL_DB_PASSWORD")
                   ?? config["Firebird:Password"]
                   ?? "masterkey";
        var charset = config["Firebird:Charset"] ?? "UTF8";
        return new FirebirdSql.Data.FirebirdClient.FbConnectionStringBuilder
        {
            UserID = user, Password = pass, Database = db, DataSource = host,
            Port = int.Parse(port), Charset = charset
        }.ToString();
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = startDir;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "app.py")) || File.Exists(Path.Combine(dir, ".env")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }
}
