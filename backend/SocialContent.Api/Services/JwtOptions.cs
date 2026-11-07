namespace SocialContent.Api.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SocialContent.Api";
    public string Audience { get; set; } = "SocialContent.Client";

    /// <summary>Klucz podpisu HS256, minimum 32 bajty. Na produkcji ustaw przez zmienną środowiskową Jwt__Key.</summary>
    public string Key { get; set; } = "";

    public int ExpiresMinutes { get; set; } = 60;
}
