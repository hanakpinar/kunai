using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Options;

using static Kunai.Api.Features.Catalog.Providers.Igdb.IgdbModels;

namespace Kunai.Api.Features.Catalog.Providers.Igdb;

/// <summary>
/// Fetches and caches the Twitch app access token (client-credentials grant) that every IGDB call needs.
/// A singleton, so all requests share one token until shortly before it expires.
/// </summary>
internal sealed partial class IgdbTokenSource(
    IHttpClientFactory httpClientFactory,
    IOptions<IgdbOptions> options,
    TimeProvider time,
    ILogger<IgdbTokenSource> logger) : IDisposable
{
    public const string ClientName = "igdb-token";

    // Twitch app tokens live about 60 days; the cap only guards against nonsense values.
    private const long MaxLifetimeSeconds = 90L * 24 * 60 * 60;

    private static readonly TimeSpan RenewBeforeExpiry = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _refresh = new(1, 1);
    private volatile CachedToken? _cached;

    /// <exception cref="HttpRequestException">Twitch did not issue a token; callers map it to provider_unavailable.</exception>
    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        if (Valid(_cached) is { } token)
        {
            return token;
        }

        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (Valid(_cached) is { } renewed)
            {
                return renewed;
            }

            var fresh = await RequestAsync(cancellationToken);
            _cached = fresh;
            return fresh.Value;
        }
        finally
        {
            _refresh.Release();
        }
    }

    /// <summary>Drops <paramref name="token"/> unless another request already replaced it.</summary>
    public void Invalidate(string token)
    {
        var current = _cached;
        if (current is not null && string.Equals(current.Value, token, StringComparison.Ordinal))
        {
            Interlocked.CompareExchange(ref _cached, null, current);
        }
    }

    public void Dispose() => _refresh.Dispose();

    private string? Valid(CachedToken? cached) =>
        cached is not null && time.GetUtcNow() < cached.RenewAt ? cached.Value : null;

    private async Task<CachedToken> RequestAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var query = $"?client_id={Uri.EscapeDataString(settings.ClientId ?? string.Empty)}"
            + $"&client_secret={Uri.EscapeDataString(settings.ClientSecret ?? string.Empty)}"
            + "&grant_type=client_credentials";

        var http = httpClientFactory.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(query, UriKind.Relative));
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogTokenRejected(logger, (int)response.StatusCode);
            throw new HttpRequestException("Twitch did not issue an IGDB access token.", inner: null, response.StatusCode);
        }

        TokenResponse? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<TokenResponse>(ProviderHttp.SnakeCase, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new HttpRequestException("Twitch answered with an unreadable token response.", exception);
        }

        if (body is not { AccessToken: { Length: > 0 } accessToken })
        {
            throw new HttpRequestException("Twitch answered without an access token.");
        }

        var lifetime = TimeSpan.FromSeconds(Math.Clamp(body.ExpiresIn, 0, MaxLifetimeSeconds));
        return new CachedToken(accessToken, time.GetUtcNow() + lifetime - RenewBeforeExpiry);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Twitch refused the IGDB token request with HTTP {Status}")]
    private static partial void LogTokenRejected(ILogger logger, int status);

    private sealed record CachedToken(string Value, DateTimeOffset RenewAt);
}
