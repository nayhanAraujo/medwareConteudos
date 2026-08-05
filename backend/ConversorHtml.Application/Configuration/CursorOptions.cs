namespace ConversorHtml.Application.Configuration;

public class CursorOptions
{
    public const string SectionName = "Cursor";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "composer-2.5";
    public bool UseFast { get; set; } = true;
    public string BridgeScriptPath { get; set; } = "agent-bridge/convert.mjs";
    public int TimeoutSeconds { get; set; } = 300;
    /// <summary>Optional override for monorepo root (defaults to parent of backend/).</summary>
    public string? RepoRoot { get; set; }
}
