using System.Net;
using System.Net.Http.Headers;

namespace Kunai.Api.Features.Catalog.Providers.Igdb;

/// <summary>
/// Adds the Twitch bearer token to IGDB requests. A 401 means the cached token was revoked or expired early,
/// so the handler drops it and retries the request once with a fresh one.
/// </summary>
internal sealed partial class IgdbAuthHandler(IgdbTokenSource tokens, ILogger<IgdbAuthHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokens.GetAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        LogTokenRejected(logger);
        response.Dispose();
        tokens.Invalidate(token);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "IGDB rejected the cached access token; retrying with a new one")]
    private static partial void LogTokenRejected(ILogger logger);
}
