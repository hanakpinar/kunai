using Bunit;

using Kunai.Web.Client.Api;
using Kunai.Web.Client.Infrastructure;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Bundle;

namespace Kunai.Web.Client.Tests.Search;

/// <summary>
/// Sits in front of the fake API and answers the matching requests only after their release, like a slow
/// response already on its way: it ignores the request's cancellation, so the page's own guards are what count.
/// </summary>
internal sealed class HoldBack : DelegatingHandler
{
    private readonly List<(Func<HttpRequestMessage, string?, bool> Matches, Task Release)> _holds = [];

    /// <summary>Swaps the context's API client for one that sends every request through a new hold-back handler.</summary>
    public static HoldBack In(BunitContext context, FakeApi api)
    {
        var holdBack = new HoldBack { InnerHandler = new CsrfHandler { InnerHandler = api } };
        var http = new HttpClient(holdBack) { BaseAddress = new Uri("https://kunai.test/") };
        context.Services.AddSingleton(new KunaiApiClient(new DefaultRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http) { BaseUrl = "https://kunai.test" }));
        return holdBack;
    }

    /// <summary>Holds back every request to <paramref name="path"/> until <paramref name="until"/> completes.</summary>
    public HoldBack Path(string path, Task until) =>
        When((request, _) => string.Equals(request.RequestUri?.AbsolutePath, path, StringComparison.Ordinal), until);

    /// <summary>Holds back every request whose body contains <paramref name="text"/> until <paramref name="until"/> completes.</summary>
    public HoldBack BodyContaining(string text, Task until) =>
        When((_, body) => body?.Contains(text, StringComparison.Ordinal) == true, until);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Reading buffers the content, so the fake API can read it again.
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(CancellationToken.None);
        foreach (var (matches, release) in _holds)
        {
            if (matches(request, body))
            {
                await release;
            }
        }

        return await base.SendAsync(request, CancellationToken.None);
    }

    private HoldBack When(Func<HttpRequestMessage, string?, bool> matches, Task until)
    {
        _holds.Add((matches, until));
        return this;
    }
}
