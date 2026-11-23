using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.Common.Results;

using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Kunai.Api.Features.Catalog.Providers;

/// <summary>Sends a provider request and maps transport failures to <see cref="ProviderErrors"/>.</summary>
public static partial class ProviderHttp
{
    /// <summary>Provider JSON is snake_case (TMDB, IGDB) or camelCase (Google Books, AniList); both bind case-insensitively.</summary>
    public static JsonSerializerOptions SnakeCase { get; } = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static JsonSerializerOptions CamelCase { get; } = new(JsonSerializerDefaults.Web);

    /// <returns>The body, <c>null</c> for 404, or an error for anything else that is not a success.</returns>
    public static async Task<Result<T?>> SendAsync<T>(
        HttpClient http,
        HttpRequestMessage request,
        ItemSource source,
        JsonSerializerOptions json,
        ILogger logger,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (T?)null;
            }

            if (!response.IsSuccessStatusCode)
            {
                LogFailure(logger, source, (int)response.StatusCode);
                return ProviderErrors.Unavailable(source);
            }

            return await response.Content.ReadFromJsonAsync<T>(json, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException or JsonException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            LogException(logger, exception, source);
            return ProviderErrors.Unavailable(source);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Source} answered with HTTP {Status}")]
    private static partial void LogFailure(ILogger logger, ItemSource source, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Source} request failed")]
    private static partial void LogException(ILogger logger, Exception exception, ItemSource source);
}
