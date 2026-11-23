using Kunai.Api.Common.OpenApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Kunai.Api.Features.Identity;

/// <summary>Describes both sign-in methods in the OpenAPI document and marks which operations need one.</summary>
internal static class AuthOpenApi
{
    public const string BearerScheme = "Bearer";
    public const string CookieScheme = "Cookie";

    public static IServiceCollection AddAuthToOpenApi(this IServiceCollection services) =>
        services.Configure<OpenApiOptions>(OpenApiSetup.DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Native apps: the access token from /auth/login or /auth/refresh.",
                };
                document.Components.SecuritySchemes[CookieScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Cookie,
                    Name = AuthenticationSetup.CookieName,
                    Description = "Web app: set by /auth/login with useCookie=true. Requests that change data must also send 'X-CSRF: 1'.",
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
                {
                    return Task.CompletedTask;
                }

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [] });
                operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(CookieScheme, context.Document)] = [] });
                return Task.CompletedTask;
            });
        });
}
