namespace IdentityPatients.Application.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SmartHealthcare";
    public string Audience { get; set; } = "SmartHealthcare";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}

