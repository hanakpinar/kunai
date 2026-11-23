namespace Kunai.Api.Features.Identity.Sessions;

/// <summary>
/// One signed-in device. Both the web cookie and the mobile tokens point at a session (the "sid" claim),
/// so logging out works the same way for both: revoke the session.
/// </summary>
public sealed class UserSession
{
    public const int DeviceNameMaxLength = 100;

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; init; }

    public ClientType ClientType { get; init; }

    public string? DeviceName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Absolute lifetime: the user signs in again after this, however active they are.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}

public enum ClientType
{
    Web,
    Mobile,
}
