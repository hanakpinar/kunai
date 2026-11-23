namespace Kunai.Api.Features.Identity;

/// <summary>Short JWT-style claim names, used for both the cookie and the bearer token.</summary>
public static class KunaiClaims
{
    public const string UserId = "sub";
    public const string SessionId = "sid";
    public const string UserName = "name";
    public const string Role = "role";
}
