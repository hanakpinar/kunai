namespace Kunai.Api.Features.Identity.Sessions;

public sealed class JwtOptions
{
    public const string SectionName = "Auth:Jwt";

    public string Issuer { get; set; } = "kunai";

    public string Audience { get; set; } = "kunai-api";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>A session ends after this long without a refresh.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(90);

    /// <summary>EC P-256 private key (PEM) for ES256. Required outside Development and Testing.</summary>
    public string? SigningKeyPem { get; set; }
}
