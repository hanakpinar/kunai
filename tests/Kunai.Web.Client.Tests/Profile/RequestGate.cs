using System.Collections.Concurrent;

namespace Kunai.Web.Client.Tests.Profile;

/// <summary>
/// Holds the requests to a path until the test lets them through, to answer them late or out of order.
/// A held request ignores cancellation on purpose: its answer still arrives, like one already on the wire.
/// </summary>
public sealed class RequestGate : DelegatingHandler
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _held = new(StringComparer.Ordinal);

    /// <summary>Holds every request to <paramref name="path"/> (no query string) from now on.</summary>
    public void Hold(string path) => _held[path] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets the held requests to <paramref name="path"/> through, and every later one.</summary>
    public void Release(string path)
    {
        if (_held.TryRemove(path, out var gate))
        {
            gate.SetResult();
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_held.TryGetValue(request.RequestUri!.AbsolutePath, out var gate))
        {
            await gate.Task;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
