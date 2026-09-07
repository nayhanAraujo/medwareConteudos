namespace MdwConteudos.Api.Infrastructure;

/// <summary>
/// Localiza a raiz da stack migrada pelo conteúdo da pasta, sem depender do nome
/// físico usado no checkout, e centraliza os caminhos compartilhados da aplicação.
/// </summary>
public static class MigrationRootResolver
{
    private const int MaxParentDepth = 12;

    public static string ResolveMigrationRoot(IConfiguration? config = null, string? startDir = null)
    {
        var start = Path.GetFullPath(startDir ?? AppContext.BaseDirectory);
        var configured = config?["LegacyPaths:RepoRoot"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            var configuredRoot = Path.IsPathFullyQualified(configured)
                ? Path.GetFullPath(configured)
                : Path.GetFullPath(configured, start);
            if (Directory.Exists(configuredRoot))
            {
                var resolved = FindMigrationRoot(configuredRoot, includeChildren: true);
                if (resolved is not null) return resolved;
            }
        }

        var detected = FindMigrationRoot(start, includeChildren: true);
        if (detected is not null) return detected;

        return Path.GetFullPath(Path.Combine(start, "..", ".."));
    }

    public static string? ResolveLegacyRoot(
        IConfiguration? config,
        string migrationRoot,
        string? startDir = null)
    {
        var configured = config?["LegacyPaths:RepoRoot"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var baseDirectory = Path.GetFullPath(startDir ?? migrationRoot);
            var full = Path.IsPathFullyQualified(configured)
                ? Path.GetFullPath(configured)
                : Path.GetFullPath(configured, baseDirectory);
            if (Directory.Exists(full) && !SamePath(full, migrationRoot)) return full;
        }

        var current = new DirectoryInfo(Path.GetFullPath(startDir ?? migrationRoot)).Parent;
        for (var depth = 0; current is not null && depth < MaxParentDepth; depth++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "app.py"))) return current.FullName;
        }

        return null;
    }

    public static string StaticRoot(string migrationRoot) => Path.Combine(migrationRoot, "static");

    public static string StaticUploadsRoot(string migrationRoot, bool create = true)
    {
        var root = Path.Combine(StaticRoot(migrationRoot), "uploads");
        if (create) Directory.CreateDirectory(root);
        return root;
    }

    public static string UploadsRoot(string migrationRoot, bool create = true)
    {
        var root = Path.Combine(migrationRoot, "uploads");
        if (create) Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>Converte LINK/CAMINHO persistido em URL servida por /static.</summary>
    public static string? ToWebUrl(string? link, string? caminho = null)
    {
        foreach (var candidate in new[] { link, caminho })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var value = candidate.Trim().Replace('\\', '/');

            if (value.StartsWith("/static/", StringComparison.OrdinalIgnoreCase)) return value;
            if (value.StartsWith("static/", StringComparison.OrdinalIgnoreCase)) return "/" + value;

            var index = value.IndexOf("/static/uploads/", StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return value[index..];

            index = value.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return "/static" + value[index..];

            if (!Path.IsPathRooted(candidate) && !value.Contains(':'))
                return $"/static/uploads/{value.TrimStart('/')}";
        }

        return null;
    }

    private static string? FindMigrationRoot(string startDir, bool includeChildren)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDir));
        for (var depth = 0; current is not null && depth < MaxParentDepth; depth++, current = current.Parent)
        {
            if (LooksLikeMigrationRoot(current.FullName)) return current.FullName;

            if (!includeChildren) continue;
            try
            {
                var child = current.EnumerateDirectories()
                    .FirstOrDefault(candidate => LooksLikeMigrationRoot(candidate.FullName));
                if (child is not null) return child.FullName;
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                // Continua subindo: algumas raízes de serviço podem não ser enumeráveis.
            }
        }

        return null;
    }

    private static bool LooksLikeMigrationRoot(string directory)
        => Directory.Exists(Path.Combine(directory, "backend", "MdwConteudos.Api"))
           && Directory.Exists(Path.Combine(directory, "frontend", "nuxt-app"));

    private static bool SamePath(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
}
