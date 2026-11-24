using AngleSharp.Dom;

using Bunit;

using Kunai.Web.Client.Api;
using Kunai.Web.Client.Infrastructure;
using Kunai.Web.Client.Pages.Profile;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Bundle;

namespace Kunai.Web.Client.Tests.Profile;

/// <summary>Canned API answers for the public profile: ninja's profile, shelf pages, stats, goals and belts.</summary>
public abstract class ProfileTestContext : KunaiTestContext
{
    protected const string ProfilePath = "/api/v1/users/ninja";

    /// <summary>Serves ninja's profile, the first shelf page (with a next cursor) and the belts.</summary>
    protected void Serve()
    {
        ServeProfile("ninja", "Kara Ninja");
        Api.On(HttpMethod.Get, $"{ProfilePath}/shelf", ShelfPage("Dune", nextCursor: "next-page"));
        Api.On(HttpMethod.Get, "/api/v1/ranks", new object[]
        {
            new { code = "white", label = new { tr = "Beyaz kuşak", en = "White belt" }, minPoints = 0, color = "#f4f4f0" },
            new { code = "green", label = new { tr = "Yeşil kuşak", en = "Green belt" }, minPoints = 100, color = "#2e9e4f" },
        });
    }

    protected void ServeProfile(string username, string displayName) =>
        Api.On(HttpMethod.Get, $"/api/v1/users/{username}", new
        {
            username,
            displayName,
            bio = "Gece okur, gündüz oynar.",
            avatarUrl = $"/api/v1/avatars/{username}-07.svg?belt=green",
            joinedAt = "2025-03-15T12:00:00Z",
            rank = new { code = "green", label = new { tr = "Yeşil kuşak", en = "Green belt" }, points = 120, nextCode = "blue", nextAt = 150 },
            counts = new[]
            {
                new { type = "book", planned = 4, inProgress = 1, completed = 42, paused = 0, dropped = 2 },
                new { type = "movie", planned = 3, inProgress = 0, completed = 0, paused = 0, dropped = 0 },
                new { type = "game", planned = 1, inProgress = 2, completed = 7, paused = 0, dropped = 0 },
            },
            thisYear = new { year = 2026, completed = new[] { new { type = "book", count = 12 }, new { type = "game", count = 3 } } },
            favorites = new[] { Item("Dune") },
        });

    /// <summary>
    /// Puts a <see cref="RequestGate"/> in front of the fake API, to answer chosen requests late.
    /// Call it before the first render: the services are fixed from then on.
    /// </summary>
    protected RequestGate UseRequestGate()
    {
        var gate = new RequestGate { InnerHandler = new CsrfHandler { InnerHandler = Api } };
        var http = new HttpClient(gate) { BaseAddress = new Uri("https://kunai.test/") };
        Services.AddSingleton(new KunaiApiClient(new DefaultRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http) { BaseUrl = "https://kunai.test" }));
        return gate;
    }

    /// <summary>Opens ninja's profile as its owner, on the Görevler tab, with the editor open.</summary>
    protected IRenderedComponent<PublicProfile> OpenGoalsEditor()
    {
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("a[href=settings]").TextContent.Should().Be("Profili düzenle"));
        ButtonNamed(page, "Görevler").Click();
        page.WaitForAssertion(() => ButtonNamed(page, "Görevleri düzenle"));
        ButtonNamed(page, "Görevleri düzenle").Click();
        page.WaitForElement("form.goals-form");
        return page;
    }

    protected static IElement ButtonNamed<TComponent>(IRenderedComponent<TComponent> page, string text)
        where TComponent : IComponent =>
        page.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    protected static bool HasButton<TComponent>(IRenderedComponent<TComponent> page, string text)
        where TComponent : IComponent =>
        page.FindAll("button").Any(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    protected IEnumerable<string> QueriesTo(string path) =>
        Api.Requests.Select(r => r.PathAndQuery).Where(p => string.Equals(p.Split('?')[0], path, StringComparison.Ordinal));

    protected static object Item(string title) => new
    {
        id = Guid.NewGuid(),
        type = "book",
        title,
        year = 1965,
        coverUrl = (string?)null,
        creators = new[] { "Frank Herbert" },
        progressTotal = 412,
    };

    protected static object ShelfPage(string title, string? nextCursor, int totalCount = 2) => new
    {
        items = new[]
        {
            new
            {
                item = Item(title),
                status = "completed",
                rating = 9,
                startedOn = "2026-01-02",
                finishedOn = "2026-02-01",
                progress = 412,
                review = (string?)null,
                isFavorite = true,
                createdAt = "2026-01-02T10:00:00Z",
                updatedAt = "2026-02-01T10:00:00Z",
            },
        },
        nextCursor,
        totalCount,
    };

    protected static object Stats(int year, int booksCompleted, params int[] availableYears) => new
    {
        year,
        availableYears,
        types = new object[]
        {
            new { type = "book", completed = booksCompleted, averageRating = 8.4 },
            new { type = "game", completed = 3, averageRating = (double?)null },
        },
        completedByMonth = new[] { 1, 2, 0, 3, 1, 0, 2, 1, 0, 1, 1, 3 },
        pagesRead = 12450,
        episodesWatched = 86,
        chaptersRead = 0,
    };

    protected static object Goals(int year, params (string Type, int Target, int Completed, int Percent)[] goals) => new
    {
        year,
        goals = goals.Select(g => new { type = g.Type, target = g.Target, completed = g.Completed, percent = g.Percent }).ToArray(),
    };
}
