namespace MdwConteudos.Api.Configuration;

public class WebAuthOptions
{
    public const string SectionName = "WebAuth";
    public string JwtSecret { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 30;
    public string Issuer { get; set; } = "MdwConteudos";
    public string Audience { get; set; } = "MdwConteudos.Web";
    public bool DevUserEnabled { get; set; } = false;
    public string DevUsername { get; set; } = "";
    public string DevPassword { get; set; } = "";
    public string DevName { get; set; } = "Desenvolvimento";
    public string DevRole { get; set; } = "admin";
}
