using System.Security.Cryptography;
using System.Text;

using Kunai.Api.Common.Results;
using Kunai.Api.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Kunai.Api.Features.Identity.Sessions;

/// <summary>Creates, refreshes and revokes sessions. Refresh tokens rotate on every use.</summary>
public sealed partial class SessionService(
    AppDbContext db,
    IMemoryCache cache,
    IOptions<JwtOptions> options,
    TimeProvider time,
    ILogger<SessionService> logger)
{
    public const string InvalidRefreshToken = "invalid_refresh_token";

    private static readonly TimeSpan ActiveCacheDuration = TimeSpan.FromSeconds(60);

    public async Task<UserSession> StartAsync(Guid userId, ClientType clientType, string? deviceName, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var session = new UserSession
        {
            UserId = userId,
            ClientType = clientType,
            DeviceName = deviceName,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.Add(options.Value.SessionLifetime),
        };
        db.Set<UserSession>().Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    /// <summary>Creates a refresh token for the session and returns the raw value; only its hash is stored.</summary>
    public async Task<string> IssueRefreshTokenAsync(UserSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = time.GetUtcNow();
        var expires = now.Add(options.Value.RefreshTokenLifetime);

        db.Set<RefreshToken>().Add(new RefreshToken
        {
            SessionId = session.Id,
            TokenHash = Hash(raw),
            CreatedAt = now,
            ExpiresAt = expires < session.ExpiresAt ? expires : session.ExpiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    /// <summary>
    /// Exchanges a refresh token for a new one. A token that was already used means it leaked
    /// (or the client refreshed twice at once): the whole session is revoked.
    /// </summary>
    public async Task<Result<(UserSession Session, string RefreshToken)>> RotateAsync(string rawToken, CancellationToken cancellationToken)
    {
        var invalid = Error.Unauthorized(InvalidRefreshToken, "The refresh token is invalid or expired. Sign in again.");
        var now = time.GetUtcNow();
        var hash = Hash(rawToken);

        var token = await db.Set<RefreshToken>().Include(t => t.Session)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null || !token.Session.IsActive(now))
        {
            return invalid;
        }

        if (token.ConsumedAt is not null)
        {
            LogReuse(logger, token.SessionId);
            await RevokeAsync(token.Session, cancellationToken);
            return invalid;
        }

        if (token.ExpiresAt <= now)
        {
            return invalid;
        }

        // Consume atomically: of two concurrent refreshes with the same token, only one wins.
        var consumed = await db.Set<RefreshToken>()
            .Where(t => t.Id == token.Id && t.ConsumedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.ConsumedAt, now), cancellationToken);
        if (consumed == 0)
        {
            LogReuse(logger, token.SessionId);
            await RevokeAsync(token.Session, cancellationToken);
            return invalid;
        }

        token.Session.LastSeenAt = now;
        var next = await IssueRefreshTokenAsync(token.Session, cancellationToken);
        return (token.Session, next);
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await db.Set<UserSession>().FindAsync([sessionId], cancellationToken);
        if (session is not null)
        {
            await RevokeAsync(session, cancellationToken);
        }
    }

    public Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken) =>
        RevokeAllExceptAsync(userId, keep: null, cancellationToken);

    /// <summary>Revokes every session of the user except <paramref name="keep"/> (the caller's own device).</summary>
    public async Task RevokeAllExceptAsync(Guid userId, Guid? keep, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var sessions = db.Set<UserSession>().Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != keep);
        var ids = await sessions.Select(s => s.Id).ToListAsync(cancellationToken);
        await sessions.ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAt, now), cancellationToken);
        foreach (var id in ids)
        {
            cache.Remove(CacheKey(id));
        }
    }

    /// <summary>Checked on every authenticated request; cached for a minute per instance.</summary>
    public async Task<bool> IsActiveAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey(sessionId), out bool active))
        {
            return active;
        }

        var now = time.GetUtcNow();
        active = await db.Set<UserSession>().AnyAsync(
            s => s.Id == sessionId && s.RevokedAt == null && s.ExpiresAt > now,
            cancellationToken);
        cache.Set(CacheKey(sessionId), active, ActiveCacheDuration);
        return active;
    }

    private async Task RevokeAsync(UserSession session, CancellationToken cancellationToken)
    {
        session.RevokedAt ??= time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey(session.Id));
    }

    private static string Hash(string raw) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string CacheKey(Guid sessionId) => "session-active:" + sessionId;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected; session {SessionId} revoked")]
    private static partial void LogReuse(ILogger logger, Guid sessionId);
}
