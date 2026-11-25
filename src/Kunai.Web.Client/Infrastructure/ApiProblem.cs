using Kunai.Web.Client.Api.Models;

using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace Kunai.Web.Client.Infrastructure;

/// <summary>
/// An API error turned into Turkish text for the UI, with per-field messages for forms.
/// <paramref name="Detail"/> is the problem's untranslated detail (e.g. the existing item id on book_exists).
/// </summary>
public sealed record ApiProblem(string Code, string Message, IReadOnlyDictionary<string, string> Fields, int Status, string? Detail = null)
{
    /// <summary>
    /// Whether trying the same request again can succeed (network, rate limit, server or provider outage).
    /// A provider without credentials answers 503 too, but stays unavailable until the server is configured.
    /// </summary>
    public bool IsRetryable => Status is 0 or 429 or >= 500 && !string.Equals(Code, "provider_not_configured", StringComparison.Ordinal);

    /// <summary>
    /// The failures an API call can end with and the UI shows as a problem: an API error, no connection, or an
    /// HttpClient timeout. Pages that cancel their own requests catch OperationCanceledException first.
    /// </summary>
    public static bool IsApiFailure(Exception exception) =>
        exception is ApiException or HttpRequestException or TaskCanceledException;

    public static ApiProblem From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            HttpValidationProblemDetails validation => Build(validation.Code, validation.ResponseStatusCode, validation.Errors?.AdditionalData, validation.Detail),
            ProblemDetails problem => Build(problem.Code, problem.ResponseStatusCode, problem.Errors?.AdditionalData, problem.Detail),
            ApiException api => Build(null, api.ResponseStatusCode, null, null),
            HttpRequestException or TaskCanceledException => new ApiProblem("network", "Sunucuya ulaşılamadı. Bağlantını kontrol edip tekrar dene.", new Dictionary<string, string>(StringComparer.Ordinal), 0),
            _ => new ApiProblem("unexpected", "Beklenmeyen bir hata oluştu.", new Dictionary<string, string>(StringComparer.Ordinal), 0),
        };
    }

    private static ApiProblem Build(string? code, int status, IDictionary<string, object>? errors, string? detail)
    {
        code ??= status switch
        {
            401 => "unauthorized",
            404 => "not_found",
            409 => "conflict",
            429 => "rate_limited",
            >= 500 => "internal_error",
            _ => "bad_request",
        };

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (field, value) in errors ?? new Dictionary<string, object>(StringComparer.Ordinal))
        {
            var first = value switch
            {
                UntypedArray array => array.GetValue().OfType<UntypedString>().Select(s => s.GetValue()).FirstOrDefault(),
                IEnumerable<object> list => list.FirstOrDefault()?.ToString(),
                _ => value?.ToString(),
            };
            if (first is not null)
            {
                fields[field] = ErrorMessages.For(first);
            }
        }

        return new ApiProblem(code, ErrorMessages.For(code), fields, status, detail);
    }
}
