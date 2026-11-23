namespace Kunai.Api.Common.Http;

public static class SecurityHeaders
{
    /// <summary>Conservative headers for a JSON API: no sniffing, no framing, no referrer leaks.</summary>
    public static IApplicationBuilder UseKunaiSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            await next(context);
        });
}
