using Kunai.Api.Common.Results;

namespace Kunai.Api.Features.Identity;

/// <summary>
/// Cookie-authenticated writes must carry an "X-CSRF" header. A cross-site form cannot set custom headers,
/// and a cross-site script would need a CORS preflight that this API never allows.
/// Bearer-token requests are not affected: a browser never attaches those automatically.
/// </summary>
internal static class CsrfProtection
{
    public const string HeaderName = "X-CSRF";

    public static IApplicationBuilder UseKunaiCsrfProtection(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            var unsafeMethod = !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method);

            if (unsafeMethod
                && request.Cookies.ContainsKey(AuthenticationSetup.CookieName)
                && !request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.Ordinal)
                && !request.Headers.ContainsKey(HeaderName))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Missing CSRF header.",
                        Detail = $"Cookie-authenticated requests that change data must send the '{HeaderName}' header.",
                        Extensions = { ["code"] = ErrorCodes.CsrfHeaderMissing },
                    },
                });
                return;
            }

            await next(context);
        });
}
