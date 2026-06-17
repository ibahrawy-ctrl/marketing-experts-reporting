namespace Reporting.Infrastructure.Identity;

/// <summary>إعدادات JWT تُقرأ من قسم "Jwt" في الإعدادات.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "reporting-api";
    public string Audience { get; set; } = "reporting-spa";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 14;
}
