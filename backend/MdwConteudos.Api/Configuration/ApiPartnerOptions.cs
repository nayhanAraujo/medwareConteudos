namespace MdwConteudos.Api.Configuration;

public class ApiPartnerOptions
{
    public const string SectionName = "ApiPartner";
    public string JwtSecret { get; set; } = "mdw-api-jwt-conteudos-secret";
    public string JwtPassword { get; set; } = "";
    public double JwtDatetimeToleranceHours { get; set; } = 24;
}
