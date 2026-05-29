namespace MdwConteudos.Api.Configuration;

public class LegacyPathsOptions
{
    public const string SectionName = "LegacyPaths";
    public string UploadFolder { get; set; } = "";
    public string UploadDocumentsFolder { get; set; } = "";
    public string ParseCsFileExe { get; set; } = "";
    public string RepoRoot { get; set; } = "";
}
