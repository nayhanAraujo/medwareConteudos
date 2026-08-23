namespace MdwConteudos.Api.Infrastructure;

/// <summary>
/// Resolve o root do repositório e a pasta <c>static/uploads</c> usada por scripts, referências e anexos.
/// </summary>
public static class StaticContentPaths
{
    public static string ResolveRepoRoot(IConfiguration? config = null, string? startDir = null)
    {
        var configured = config?["LegacyPaths:RepoRoot"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var full = Path.GetFullPath(configured);
            if (Directory.Exists(full)) return full;
        }

        var dir = startDir ?? Directory.GetCurrentDirectory();
        for (var i = 0; i < 10; i++)
        {
            if (LooksLikeRepoRoot(dir)) return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        return Path.GetFullPath(startDir ?? Directory.GetCurrentDirectory());
    }

    public static string StaticRoot(string repoRoot) => Path.Combine(repoRoot, "static");

    public static string UploadsRoot(string repoRoot)
    {
        var root = Path.Combine(StaticRoot(repoRoot), "uploads");
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// Converte LINK/CAMINHO do banco em URL web servida em /static/...
    /// </summary>
    public static string? ToWebUrl(string? link, string? caminho = null)
    {
        foreach (var candidate in new[] { link, caminho })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var value = candidate.Trim().Replace('\\', '/');

            if (value.StartsWith("/static/", StringComparison.OrdinalIgnoreCase))
                return value;

            var idx = value.IndexOf("/static/uploads/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return value[idx..];

            idx = value.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return "/static" + value[idx..];

            // Caminho relativo tipo CARDIOLOGIA/arquivo.pdf
            if (!Path.IsPathRooted(candidate) && !value.Contains(':'))
                return $"/static/uploads/{value.TrimStart('/')}";
        }

        return null;
    }

    private static bool LooksLikeRepoRoot(string dir)
        => File.Exists(Path.Combine(dir, ".env"))
           || Directory.Exists(Path.Combine(dir, "static", "uploads"))
           || File.Exists(Path.Combine(dir, "BD", "REFERENCIAS.FDB"))
           || File.Exists(Path.Combine(dir, "app.py"));
}
