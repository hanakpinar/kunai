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

    /// <summary>
    /// Holds back the answer to the next <paramref name="method"/> request to <paramref name="path"/>, or to the next
    /// one whose query contains <paramref name="query"/>.
    /// </summary>
    public HeldAnswer HoldNext(HttpMethod method, string path, string? query = null)
    {
        var held = new HeldAnswer(Key(method, path), query);
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
        var query = request.RequestUri.Query;
        HeldAnswer? held;
        lock (_gate)
        {
            if (_failures.TryGetValue(key, out var failure))
            {
                throw failure;
            }

            held = _holds.Find(h => string.Equals(h.Key, key, StringComparison.Ordinal)
                && (h.Query is null || query.Contains(h.Query, StringComparison.Ordinal)));
            if (held is not null)
            {
                _holds.Remove(held);
            }
        }

        // FakeApi answers with the routes registered now; a held answer then waits for the test. Like a real network,
        // nothing here runs on the renderer's thread (ConfigureAwait(false)): a released answer comes back even
        // while the page is busy.
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (held is null)
        {
            return response;
        }

        // Tell the test when the client has read the whole answer.
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var content = new StreamContent(new TrackedBody(body, held));
        foreach (var header in response.Content.Headers)
        {
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content.Dispose();
        response.Content = content;
        await held.WaitAsync(cancellationToken).ConfigureAwait(false);
        return response;
    }

    private static string Key(HttpMethod method, string path) => $"{method.Method} {path}";

    // A held answer's body: marks the answer read once the client reaches its end.
    private sealed class TrackedBody(byte[] body, HeldAnswer held) : MemoryStream(body, writable: false)
    {
        public override int Read(byte[] buffer, int offset, int count) => Track(base.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Track(base.Read(buffer));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Track(await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Track(await base.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false));

        public override void CopyTo(Stream destination, int bufferSize)
        {
            base.CopyTo(destination, bufferSize);
            Track(0);
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await base.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
            Track(0);
        }

        private int Track(int read)
        {
            if (Position >= Length)
            {
                held.MarkRead();
            }

            return read;
        }
    }
}

/// <summary>
/// One answer <see cref="SlowNetwork"/> holds back until <see cref="Release"/>. Holding renders nothing, so a test
/// awaits <see cref="Waiting"/> and <see cref="Delivered"/> instead of polling with WaitForAssertion.
/// </summary>
internal sealed class HeldAnswer(string key, string? query)
{
    // Long enough for the test to start the next load, short enough that a synchronous Cancel() only waits it out.
    private static readonly TimeSpan SlowCancellationBound = TimeSpan.FromSeconds(1);

    private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _cancellationOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _slowCancellation;
    private volatile bool _wasCancelled;

    public string Key { get; } = key;

    /// <summary>Only a request whose query contains this is held; null holds the next request to the path.</summary>
    public string? Query { get; } = query;

    /// <summary>Completes when the request has arrived and its answer is waiting.</summary>
    public Task Waiting => _waiting.Task;

    /// <summary>Completes when the answer has gone back to the client.</summary>
    public Task Delivered => _delivered.Task;

    /// <summary>Completes when the client has read the answer's whole body.</summary>
    public Task Read => _read.Task;

    /// <summary>The client cancelled the request while its answer waited.</summary>
    public bool WasCancelled => _wasCancelled;

    public void Release() => _released.TrySetResult();

    /// <summary>
    /// Makes cancelling this request slow, like an abort that has to reach the browser: the cancellation callback
    /// blocks (at most a second) until <see cref="OpenCancellation"/>. A client that awaits
    /// CancellationTokenSource.CancelAsync stays suspended meanwhile; a synchronous Cancel() waits it out.
    /// </summary>
    public void SlowDownCancellation() => _slowCancellation = true;

    public void OpenCancellation() => _cancellationOpened.TrySetResult();

    internal void MarkRead() => _read.TrySetResult();

    // Ignores the cancellation on purpose: the answer still arrives, as when the server replied before the abort.
    internal async Task WaitAsync(CancellationToken cancellationToken)
    {
        using (cancellationToken.Register(OnCancelled))
        {
            _waiting.TrySetResult();
            await _released.Task.ConfigureAwait(false);
        }

        _delivered.TrySetResult();
    }

    private void OnCancelled()
    {
        _wasCancelled = true;
        if (_slowCancellation)
        {
            _cancellationOpened.Task.Wait(SlowCancellationBound);
        }
    }
}
