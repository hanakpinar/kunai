using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Kunai.Web.Client.Tests.Infrastructure;

/// <summary>
/// Stands in for the Kunai API behind the generated client: register a canned JSON answer per method and
/// path, then assert on the requests the component sent. Unregistered calls answer 404 problem details.
/// </summary>
public sealed class FakeApi : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new(StringComparer.Ordinal);

    public ConcurrentQueue<(HttpMethod Method, string PathAndQuery, string? Body)> Requests { get; } = new();

    /// <summary>
    /// When true, every answer arrives after a yield, the way a browser's fetch does. Use it to catch code that only
    /// works because the fake answers synchronously (assert with WaitForAssertion / WaitForState).
    /// </summary>
    public bool AnswerAsynchronously { get; set; }

    /// <summary>Answers <paramref name="method"/> requests to <paramref name="path"/> (no query string) with JSON.</summary>
    public FakeApi On(HttpMethod method, string path, object? body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[Key(method, path)] = _ => Respond(status, body, "application/json");
        return this;
    }

    /// <summary>Answers with an RFC 9457 problem carrying the API's stable error code.</summary>
    public FakeApi OnProblem(HttpMethod method, string path, HttpStatusCode status, string code, IDictionary<string, string[]>? errors = null)
    {
        _routes[Key(method, path)] = _ => Respond(status, new { status = (int)status, code, errors }, "application/problem+json");
        return this;
    }

    public IEnumerable<string?> BodiesSentTo(HttpMethod method, string path) =>
        Requests.Where(r => r.Method == method && string.Equals(r.PathAndQuery.Split('?')[0], path, StringComparison.Ordinal)).Select(r => r.Body);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        if (AnswerAsynchronously)
        {
            await Task.Yield();
        }

        Requests.Enqueue((request.Method, request.RequestUri!.PathAndQuery, body));
        return _routes.TryGetValue(Key(request.Method, request.RequestUri.AbsolutePath), out var respond)
            ? respond(request)
            : Respond(HttpStatusCode.NotFound, new { status = 404, code = "not_found" }, "application/problem+json");
    }

    private static string Key(HttpMethod method, string path) => $"{method.Method} {path}";

    private static HttpResponseMessage Respond(HttpStatusCode status, object? body, string contentType) => new(status)
    {
        Content = body is null ? null : new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, contentType),
    };
}
