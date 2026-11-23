using System.Security.Claims;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kunai.Api.Features.Identity.Sessions;

/// <summary>Issues short-lived ES256 access tokens for mobile clients.</summary>
public sealed class AccessTokenIssuer(JwtSigningKey key, IOptions<JwtOptions> options, TimeProvider time)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, int ExpiresInSeconds) Issue(AppUser user, Guid sessionId, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        var settings = options.Value;
        var now = time.GetUtcNow().UtcDateTime;

        var claims = new List<Claim>
        {
            new(KunaiClaims.UserId, user.Id.ToString()),
            new(KunaiClaims.SessionId, sessionId.ToString()),
            new(KunaiClaims.UserName, user.UserName!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(KunaiClaims.Role, role)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(settings.AccessTokenLifetime),
            SigningCredentials = key.Credentials,
        });

        return (token, (int)settings.AccessTokenLifetime.TotalSeconds);
    }
}
