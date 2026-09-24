namespace MdwConteudos.Api.Configuration;

public class ApiPartnerOptions
{
    public const string SectionName = "ApiPartner";
    public string JwtSecret { get; set; } = "";
    public string JwtPassword { get; set; } = "";
    public double JwtDatetimeToleranceHours { get; set; } = 24;

    // Temporary migration only. New tokens are always signed with the raw key.
    public bool AcceptLegacyHashedKey { get; set; }
    public string? JwtLegacyHashedKeyUntilUtc { get; set; }

    public bool CanAcceptLegacyHashedKey(DateTimeOffset now)
    {
        var deadline = JwtLegacyHashedKeyUntilUtc?.Trim();
        return AcceptLegacyHashedKey && deadline is not null
            && (deadline.EndsWith('Z') || deadline.EndsWith("+00:00", StringComparison.Ordinal))
            && DateTimeOffset.TryParse(deadline, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var until)
            && until.Offset == TimeSpan.Zero && now < until;
    }
}
