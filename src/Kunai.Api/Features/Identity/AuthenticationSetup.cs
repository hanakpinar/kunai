using System.Security.Claims;

using Kunai.Api.Features.Identity.Sessions;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kunai.Api.Features.Identity;

/// <summary>
/// Two ways in, one session model: the web app uses an httpOnly cookie, native apps use a bearer token.
/// A policy scheme picks the handler per request.
/// </summary>
public static class AuthenticationSetup
{
    public const string Scheme = "kunai";
    public const string CookieName = "__Host-kunai";

    public static IHostApplicationBuilder AddKunaiAuthentication(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.AddSingleton(services => JwtSigningKey.Create(
            services.GetRequiredService<IOptions<JwtOptions>>().Value,
            services.GetRequiredService<IHostEnvironment>()));
        builder.Services.AddSingleton<AccessTokenIssuer>();
        builder.Services.AddScoped<SessionService>();
        builder.Services.AddKeyedSingleton<IMemoryCache>(SessionService.CacheKeyName, (_, _) => new MemoryCache(new MemoryCacheOptions()));
        builder.Services.AddAuthToOpenApi();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.UserIdClaimType = KunaiClaims.UserId;
            options.ClaimsIdentity.UserNameClaimType = KunaiClaims.UserName;
            options.ClaimsIdentity.RoleClaimType = KunaiClaims.Role;
        });

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = Scheme;
                options.DefaultChallengeScheme = Scheme;
            })
            .AddPolicyScheme(Scheme, "Cookie or bearer token", options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.Ordinal)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : IdentityConstants.ApplicationScheme)
            .AddJwtBearer()
            .AddIdentityCookies();

        ConfigureBearer(builder.Services);
        ConfigureCookie(builder.Services);

        // Secure by default: every endpoint requires a signed-in user unless it opts out with AllowAnonymous.
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Moderator, policy => policy.RequireRole(Roles.Moderator, Roles.Admin));

        return builder;
    }

    private static void ConfigureBearer(IServiceCollection services)
    {
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSigningKey, IOptions<JwtOptions>>((options, key, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = key.Key,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = KunaiClaims.UserName,
                    RoleClaimType = KunaiClaims.Role,
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = SessionGuard.ValidateTokenAsync };
            });
    }

    private static void ConfigureCookie(IServiceCollection services)
    {
        // Identity re-validates the security stamp every 30 minutes and rebuilds the principal; carry the
        // session id over, or the session guard would sign the user out.
        services.Configure<SecurityStampValidatorOptions>(options => options.OnRefreshingPrincipal = context =>
        {
            if (context.CurrentPrincipal?.FindFirst(KunaiClaims.SessionId) is { } sid
                && context.NewPrincipal?.Identity is ClaimsIdentity identity
                && !identity.HasClaim(c => string.Equals(c.Type, KunaiClaims.SessionId, StringComparison.Ordinal)))
            {
                identity.AddClaim(new Claim(KunaiClaims.SessionId, sid.Value));
            }

            return Task.CompletedTask;
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // An API answers with status codes, never with redirects to a login page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };

            var validateSecurityStamp = options.Events.OnValidatePrincipal;
            options.Events.OnValidatePrincipal = async context =>
            {
                await SessionGuard.ValidateCookieAsync(context);
                if (context.Principal is not null)
                {
                    await validateSecurityStamp(context);
                }
            };
        });
    }
}
