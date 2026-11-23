namespace Kunai.Api.Features.Identity.Sessions;

/// <summary>
/// A single-use refresh token. Only the SHA-256 hash is stored; the token itself has 256 bits of entropy,
/// so a slow password hash is not needed.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid SessionId { get; init; }

    public UserSession Session { get; init; } = null!;

    public required string TokenHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Set when the token is exchanged. Presenting a consumed token again revokes the session.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
