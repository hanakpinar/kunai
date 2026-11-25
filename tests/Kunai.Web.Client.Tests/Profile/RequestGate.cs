using System.Collections.Concurrent;

namespace Kunai.Web.Client.Tests.Profile;

/// <summary>
/// Holds the requests to a path until the test lets them through, to answer them late or out of order.
/// By default a held request ignores cancellation, like one whose answer is already on the wire. Held with
/// <c>honourCancellation</c>, it ends when its caller cancels it, the way a browser aborts the fetch.
/// </summary>
public sealed class RequestGate : DelegatingHandler
{
    private readonly ConcurrentDictionary<string, (TaskCompletionSource Gate, bool HonourCancellation)> _held = new(StringComparer.Ordinal);

    /// <summary>Holds every request to <paramref name="path"/> (no query string) from now on.</summary>
    /// <param name="path">The request path.</param>
    /// <param name="honourCancellation">True to end a held request with a cancellation when its caller cancels it.</param>
    public void Hold(string path, bool honourCancellation = false) =>
        _held[path] = (new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), honourCancellation);

    /// <summary>Lets the held requests to <paramref name="path"/> through, and every later one.</summary>
    public void Release(string path)
    {
        if (_held.TryRemove(path, out var held))
        {
            held.Gate.SetResult();
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_held.TryGetValue(request.RequestUri!.AbsolutePath, out var held))
        {
            await (held.HonourCancellation ? held.Gate.Task.WaitAsync(cancellationToken) : held.Gate.Task);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
