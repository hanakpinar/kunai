namespace Kunai.Web.Client.Tests.Shelf;

/// <summary>
/// Sits between the generated client and FakeApi and plays a slow or broken network: it holds an answer back
/// until the test lets it through, or fails a request the way an HttpClient timeout does.
/// </summary>
internal sealed class SlowNetwork(HttpMessageHandler api) : DelegatingHandler(api)
{
    private readonly Lock _gate = new();
    private readonly List<HeldAnswer> _holds = [];
    private readonly Dictionary<string, Exception> _failures = new(StringComparer.Ordinal);

    /// <summary>Holds back the answer to the next <paramref name="method"/> request to <paramref name="path"/>.</summary>
    public HeldAnswer HoldNext(HttpMethod method, string path)
    {
        var held = new HeldAnswer(Key(method, path));
        lock (_gate)
        {
            _holds.Add(held);
        }

        return held;
    }

    /// <summary>Makes every request to <paramref name="path"/> throw <paramref name="exception"/> until <see cref="Recover"/>.</summary>
    public void Fail(HttpMethod method, string path, Exception exception)
    {
        lock (_gate)
        {
            _failures[Key(method, path)] = exception;
        }
    }

    public void Recover(HttpMethod method, string path)
    {
        lock (_gate)
        {
            _failures.Remove(Key(method, path));
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = Key(request.Method, request.RequestUri!.AbsolutePath);
        HeldAnswer? held;
        lock (_gate)
        {
            if (_failures.TryGetValue(key, out var failure))
            {
                throw failure;
            }

            held = _holds.Find(h => string.Equals(h.Key, key, StringComparison.Ordinal));
            if (held is not null)
            {
                _holds.Remove(held);
            }
        }

        // FakeApi answers with the routes registered now; a held answer then waits for the test.
        var response = await base.SendAsync(request, cancellationToken);
        if (held is not null)
        {
            await held.WaitAsync(cancellationToken);
        }

        return response;
    }

    private static string Key(HttpMethod method, string path) => $"{method.Method} {path}";
}

/// <summary>
/// One answer <see cref="SlowNetwork"/> holds back until <see cref="Release"/>. Holding renders nothing, so a test
/// awaits <see cref="Waiting"/> and <see cref="Delivered"/> instead of polling with WaitForAssertion.
/// </summary>
internal sealed class HeldAnswer(string key)
{
    private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _wasCancelled;

    public string Key { get; } = key;

    /// <summary>Completes when the request has arrived and its answer is waiting.</summary>
    public Task Waiting => _waiting.Task;

    /// <summary>Completes when the answer has gone back to the client.</summary>
    public Task Delivered => _delivered.Task;

    /// <summary>The client cancelled the request while its answer waited.</summary>
    public bool WasCancelled => _wasCancelled;

    public void Release() => _released.TrySetResult();

    // Ignores the cancellation on purpose: the answer still arrives, as when the server replied before the abort.
    internal async Task WaitAsync(CancellationToken cancellationToken)
    {
        using (cancellationToken.Register(() => _wasCancelled = true))
        {
            _waiting.TrySetResult();
            await _released.Task;
        }

        _delivered.TrySetResult();
    }
}
