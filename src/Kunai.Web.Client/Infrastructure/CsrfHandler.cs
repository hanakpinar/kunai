namespace Kunai.Web.Client.Infrastructure;

/// <summary>The API refuses cookie-authenticated writes without this header (a cross-site form cannot send it).</summary>
public sealed class CsrfHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            request.Headers.TryAddWithoutValidation("X-CSRF", "1");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
