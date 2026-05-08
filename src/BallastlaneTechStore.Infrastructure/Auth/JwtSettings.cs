namespace BallastlaneTechStore.Infrastructure.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ballastlane-tech-store";
    public string Audience { get; set; } = "ballastlane-tech-store-clients";
    public string SigningKey { get; set; } = default!;
    public int ExpiresMinutes { get; set; } = 480;
}
