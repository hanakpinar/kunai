using System.Net;

using Bunit;

using Kunai.Web.Client.Api;
using Kunai.Web.Client.Infrastructure;
using Kunai.Web.Client.Pages.Shelf;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Bundle;

namespace Kunai.Web.Client.Tests.Shelf;

public sealed class MyShelfTests : KunaiTestContext
{
    private const string ShelfPath = "/api/v1/me/shelf";
    private const string ItemTypesPath = "/api/v1/item-types";
    private static readonly Guid DuneId = Guid.Parse("0b6f5a9e-2a43-4d6c-9a43-6a2f0b1c7d01");
    private static readonly Guid HadesId = Guid.Parse("5c1e7f20-8d3b-4b0e-b7a5-2f9c4e6d8a02");

    public MyShelfTests()
    {
        // Answer after a yield, like a browser, so nothing passes only because the fake answers synchronously.
        Api.AnswerAsynchronously = true;
    }

    [Fact]
    public void Shows_the_entries_with_their_status_rating_progress_and_the_total()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));

        var page = Render<MyShelf>();

        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));
        page.Find(".shelf-header__count").TextContent.Should().Be("2 içerik");
        var dune = page.FindAll(".item-card")[0];
        dune.QuerySelector(".item-card__title")!.TextContent.Should().Be("Dune");
        dune.QuerySelector("a")!.GetAttribute("href").Should().Be($"items/{DuneId}");
        dune.QuerySelector(".shelf-card__rating")!.TextContent.Should().EndWith("★ 8");
        dune.QuerySelector(".shelf-card__favorite").Should().NotBeNull();
        page.WaitForAssertion(() => page.FindAll(".item-card")[0].QuerySelector(".shelf-card__progress")!.TextContent.Should().EndWith("120 / 412 sayfa"));
        page.WaitForAssertion(() => page.FindAll(".status-badge").Select(b => b.TextContent).Should().Equal("Okuyorum", "Bitirdim"));
        page.FindAll(".item-card")[1].QuerySelector(".shelf-card__progress")!.TextContent.Should().EndWith("%100");
    }

    [Fact]
    public void The_first_page_asks_for_24_entries_without_a_cursor()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Dune()));

        var page = Render<MyShelf>();

        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));
        var request = ShelfRequests().Should().ContainSingle().Subject;
        request.Should().Contain("Limit=24").And.Contain("Sort=-updatedAt").And.NotContain("Cursor=").And.NotContain("Status=");
    }

    [Fact]
    public void Picking_a_status_keeps_the_filters_in_the_url_and_reloads_with_them()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Dune()));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("shelf?type=book&favorite=true&sort=-rating");
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));

        page.FindAll(".chips button").Single(b => HasText(b, "Okuyorum")).Click();

        page.WaitForAssertion(() => navigation.Uri.Should().Contain("type=book").And.Contain("favorite=true").And.Contain("sort=-rating").And.Contain("status=inProgress"));
        page.WaitForAssertion(() => ShelfRequests().Should().HaveCount(2));
        ShelfRequests()[1].Should().Contain("Type=book").And.Contain("Status=inProgress").And.Contain("Favorite=true")
            .And.Contain("Sort=-rating").And.Contain("Limit=24");
        page.WaitForAssertion(() => page.Find(".chips button[aria-pressed=true]").TextContent.Should().Be("Okuyorum"));
    }

    [Fact]
    public void Statuses_the_type_does_not_have_are_left_out_of_the_request()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 0));
        Services.GetRequiredService<NavigationManager>().NavigateTo("shelf?type=movie&status=inProgress");

        var page = Render<MyShelf>();

        page.WaitForAssertion(() => ShelfRequests().Should().ContainSingle());
        ShelfRequests()[0].Should().Contain("Type=movie").And.NotContain("Status=");
        page.WaitForAssertion(() => page.FindAll(".chips button").Select(b => b.TextContent).Should().Equal("İzleyeceğim", "İzledim"));
        page.FindAll(".chips button[aria-pressed=true]").Should().BeEmpty();
    }

    [Fact]
    public async Task A_deep_link_shows_its_filter_while_the_type_statuses_load()
    {
        var network = UseSlowNetwork();
        var catalog = network.HoldNext(HttpMethod.Get, ItemTypesPath);
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Dune()));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("shelf?type=book&status=inProgress");

        var page = Render<MyShelf>();

        await ArrivedAsync(catalog.Waiting);
        page.WaitForAssertion(() => page.Find(".tabs button[aria-pressed=true]").TextContent.Should().Be("Kitap"));
        page.Find(".chips button[aria-pressed=true]").TextContent.Should().Be("Devam eden");

        // A click before the statuses arrive builds on the URL's filter, not on an empty one.
        page.Find(".shelf-filters__favorite").Click();
        catalog.Release();

        page.WaitForAssertion(() => navigation.Uri.Should().Contain("type=book").And.Contain("status=inProgress").And.Contain("favorite=true"));
        page.WaitForAssertion(() => page.Find(".chips button[aria-pressed=true]").TextContent.Should().Be("Okuyorum"));
        page.WaitForAssertion(() => ShelfRequests()[^1].Should().Contain("Type=book").And.Contain("Status=inProgress").And.Contain("Favorite=true"));
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));
    }

    [Fact]
    public void More_appends_the_next_page_using_the_cursor()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page("page-2", 2, Dune()));
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));

        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Hades()));
        page.FindAll("button").Single(b => HasText(b, "Daha fazla")).Click();

        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));
        ShelfRequests()[1].Should().Contain("Cursor=page-2");
        page.FindAll("button").Should().NotContain(b => HasText(b, "Daha fazla"));
    }

    [Fact]
    public void An_entry_repeated_on_the_next_page_is_shown_once()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page("page-2", 2, Dune()));
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));

        // Dune moved down after an edit and comes back on the second page.
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));
        page.FindAll("button").Single(b => HasText(b, "Daha fazla")).Click();

        page.WaitForAssertion(() => Titles(page).Should().Equal("Dune", "Hades"));
    }

    [Fact]
    public void A_filter_change_after_more_starts_again_from_the_first_page()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page("page-2", 2, Dune()));
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Hades()));
        page.FindAll("button").Single(b => HasText(b, "Daha fazla")).Click();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));

        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Hades()));
        page.FindAll(".chips button").Single(b => HasText(b, "Tamamlanan")).Click();

        page.WaitForAssertion(() => ShelfRequests().Should().HaveCount(3));
        ShelfRequests()[2].Should().Contain("Status=completed").And.NotContain("Cursor=");
        page.WaitForAssertion(() => Titles(page).Should().Equal("Hades"));
        page.Find(".shelf-header__count").TextContent.Should().Be("1 içerik");
    }

    [Fact]
    public async Task An_older_answer_never_replaces_the_newer_list()
    {
        var network = UseSlowNetwork();
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Dune()));
        var first = network.HoldNext(HttpMethod.Get, ShelfPath);
        var page = Render<MyShelf>();
        await ArrivedAsync(first.Waiting);

        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Hades()));
        page.FindAll(".chips button").Single(b => HasText(b, "Tamamlanan")).Click();

        page.WaitForAssertion(() => Titles(page).Should().Equal("Hades"));
        first.WasCancelled.Should().BeTrue("the newer filter cancels the load it replaces");

        // The stale answer still arrives in full; the first visit's lifecycle re-renders once it has handled it.
        var renders = page.RenderCount;
        first.Release();
        page.WaitForState(() => page.RenderCount > renders);
        Titles(page).Should().Equal("Hades");
        page.Find(".shelf-header__count").TextContent.Should().Be("1 içerik");
    }

    [Fact]
    public async Task An_answer_read_just_before_a_newer_filter_starts_is_dropped()
    {
        var network = UseSlowNetwork();
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Dune()));
        var first = network.HoldNext(HttpMethod.Get, ShelfPath);
        var page = Render<MyShelf>();
        await ArrivedAsync(first.Waiting);
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Hades()));

        await DeliverAsTheNextFilterStartsAsync(page, first, "Tamamlanan");

        page.WaitForAssertion(() => Titles(page).Should().Equal("Hades"));
        page.Find(".shelf-header__count").TextContent.Should().Be("1 içerik");
    }

    [Fact]
    public async Task An_error_read_just_before_a_newer_filter_starts_is_not_shown()
    {
        var network = UseSlowNetwork();
        Api.OnProblem(HttpMethod.Get, ShelfPath, HttpStatusCode.InternalServerError, "internal_error");
        var first = network.HoldNext(HttpMethod.Get, ShelfPath);
        var page = Render<MyShelf>();
        await ArrivedAsync(first.Waiting);
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Hades()));

        await DeliverAsTheNextFilterStartsAsync(page, first, "Tamamlanan");

        page.WaitForAssertion(() => Titles(page).Should().Equal("Hades"));
        page.FindAll(".alert--error").Should().BeEmpty("the error belongs to a list that is no longer shown");
    }

    [Fact]
    public async Task Filters_changed_in_quick_succession_each_cancel_the_load_before()
    {
        var network = UseSlowNetwork();
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Dune()));
        var first = network.HoldNext(HttpMethod.Get, ShelfPath);
        first.SlowDownCancellation();
        var navigation = Services.GetRequiredService<NavigationManager>();
        var page = Render<MyShelf>();
        await ArrivedAsync(first.Waiting);
        // The two filter loads in arrival order (a load that waited on its cancellation sends its request late).
        var second = network.HoldNext(HttpMethod.Get, ShelfPath, "Status=");
        var third = network.HoldNext(HttpMethod.Get, ShelfPath, "Status=");
        var last = network.HoldNext(HttpMethod.Get, ShelfPath, "Status=completed");

        // The next filter arrives while the first load's cancellation is still on its way.
        navigation.NavigateTo("shelf?status=planned");
        navigation.NavigateTo("shelf?status=inProgress");
        first.OpenCancellation();
        await ArrivedAsync(second.Waiting);
        await ArrivedAsync(third.Waiting);
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Hades()));
        navigation.NavigateTo("shelf?status=completed");
        await ArrivedAsync(last.Waiting);

        first.WasCancelled.Should().BeTrue();
        second.WasCancelled.Should().BeTrue("a load is never left running uncancelled behind a newer one");
        third.WasCancelled.Should().BeTrue("a load is never left running uncancelled behind a newer one");
        last.WasCancelled.Should().BeFalse();
        first.Release();
        second.Release();
        third.Release();
        last.Release();
        page.WaitForAssertion(() => Titles(page).Should().Equal("Hades"));
        page.Find(".shelf-header__count").TextContent.Should().Be("1 içerik");
    }

    [Fact]
    public void A_type_tab_still_works_when_the_item_types_time_out()
    {
        var network = UseSlowNetwork();
        network.Fail(HttpMethod.Get, ItemTypesPath, new TaskCanceledException("HttpClient.Timeout elapsed.", new TimeoutException()));
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Dune()));
        Services.GetRequiredService<NavigationManager>().NavigateTo("shelf?type=book");

        var page = Render<MyShelf>();

        // Neutral chips, the bare progress number and a neutral badge instead of a broken page.
        page.WaitForAssertion(() => Titles(page).Should().Equal("Dune"));
        ShelfRequests().Should().ContainSingle().Which.Should().Contain("Type=book");
        page.Find(".tabs button[aria-pressed=true]").TextContent.Should().Be("Kitap");
        page.FindAll(".chips button").Select(b => b.TextContent).Should().Equal("Planlanan", "Devam eden", "Tamamlanan", "Ara verilen", "Bırakılan");
        page.WaitForAssertion(() => page.Find(".shelf-card__progress").TextContent.Should().EndWith("İlerleme: 120 / 412"));
        page.WaitForAssertion(() => page.Find(".status-badge").TextContent.Should().Be("Devam eden"));
        page.FindAll(".alert--error").Should().BeEmpty();
    }

    [Fact]
    public void The_editor_offers_a_retry_when_the_item_types_time_out_and_the_shelf_stays_usable()
    {
        var network = UseSlowNetwork();
        network.Fail(HttpMethod.Get, ItemTypesPath, new TaskCanceledException("HttpClient.Timeout elapsed.", new TimeoutException()));
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".status-badge").Select(b => b.TextContent).Should().Equal("Devam eden", "Tamamlanan"));

        page.FindAll(".shelf-card__edit")[0].Click();

        page.WaitForAssertion(() => page.Find(".dialog .alert--error").TextContent.Should().Contain("Sunucuya ulaşılamadı"));
        network.Recover(HttpMethod.Get, ItemTypesPath);
        page.FindAll(".dialog button").Single(b => HasText(b, "Tekrar dene")).Click();
        page.WaitForAssertion(() => page.Find(".dialog select").TextContent.Should().Contain("Okuyorum"));
        page.FindAll(".dialog .alert--error").Should().BeEmpty();
        Titles(page).Should().Equal("Dune", "Hades");
    }

    [Fact]
    public void Saving_in_the_editor_updates_the_card_in_place()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));
        Api.On(HttpMethod.Put, $"{ShelfPath}/{DuneId}", Entry(DuneId, "book", "Dune", "completed", rating: 10, progress: 412, progressTotal: 412, favorite: true));
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));

        page.FindAll(".shelf-card__edit")[0].Click();
        page.WaitForElement(".dialog form").Submit();

        page.WaitForAssertion(() => page.Find(".alert--success").TextContent.Should().Be("Dune güncellendi."));
        page.FindAll(".dialog").Should().BeEmpty();
        Titles(page).Should().Equal("Dune", "Hades");
        page.Find(".shelf-header__count").TextContent.Should().Be("2 içerik");
        var dune = page.FindAll(".item-card")[0];
        dune.QuerySelector(".shelf-card__rating")!.TextContent.Should().EndWith("★ 10");
        page.WaitForAssertion(() => page.FindAll(".status-badge").Select(b => b.TextContent).Should().Equal("Okudum", "Bitirdim"));
        page.WaitForAssertion(() => page.FindAll(".item-card")[0].QuerySelector(".shelf-card__progress")!.TextContent.Should().EndWith("412 / 412 sayfa"));
        Api.BodiesSentTo(HttpMethod.Put, $"{ShelfPath}/{DuneId}").Should().ContainSingle();
    }

    [Fact]
    public void Removing_an_entry_in_the_editor_drops_its_card_and_the_count()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));
        Api.On(HttpMethod.Delete, $"{ShelfPath}/{DuneId}", null, HttpStatusCode.NoContent);
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));

        RemoveFirstCard(page);

        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));
        page.Find(".shelf-header__count").TextContent.Should().Be("1 içerik");
        page.Find(".alert--success").TextContent.Should().Be("Dune raftan kaldırıldı.");
        page.FindAll(".dialog").Should().BeEmpty();
        Api.Requests.Should().Contain(r => r.Method == HttpMethod.Delete && r.PathAndQuery == $"{ShelfPath}/{DuneId}");
    }

    [Fact]
    public void Removing_the_last_loaded_card_loads_the_rest_instead_of_an_empty_shelf()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page("page-2", 3, Dune()));
        Api.On(HttpMethod.Delete, $"{ShelfPath}/{DuneId}", null, HttpStatusCode.NoContent);
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));

        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Hades()));
        RemoveFirstCard(page);

        page.WaitForAssertion(() => Titles(page).Should().Equal("Hades"));
        page.Find(".shelf-header__count").TextContent.Should().Be("2 içerik");
        page.Find(".alert--success").TextContent.Should().Be("Dune raftan kaldırıldı.");
        page.FindAll(".empty-state").Should().BeEmpty();
        ShelfRequests()[1].Should().NotContain("Cursor=");
    }

    [Fact]
    public void Going_back_to_another_filter_closes_the_editor()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));
        var navigation = Services.GetRequiredService<NavigationManager>();
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));
        page.FindAll(".shelf-card__edit")[0].Click();
        page.WaitForElement(".dialog form");

        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Hades()));
        navigation.NavigateTo("shelf?type=game");

        page.WaitForAssertion(() => Titles(page).Should().Equal("Hades"));
        page.FindAll(".dialog").Should().BeEmpty();
    }

    [Fact]
    public async Task A_save_that_lands_after_the_list_reloaded_still_updates_its_card()
    {
        var network = UseSlowNetwork();
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));
        Api.On(HttpMethod.Put, $"{ShelfPath}/{DuneId}", Entry(DuneId, "book", "Dune", "completed", rating: 10, progress: 412, progressTotal: 412, favorite: true));
        var navigation = Services.GetRequiredService<NavigationManager>();
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));
        page.FindAll(".shelf-card__edit")[0].Click();
        var save = network.HoldNext(HttpMethod.Put, $"{ShelfPath}/{DuneId}");
        page.WaitForElement(".dialog form").Submit();
        await ArrivedAsync(save.Waiting);

        // Back to the previous sort: the same entries come back as new objects and the editor closes.
        navigation.NavigateTo("shelf?sort=-rating");
        page.WaitForAssertion(() => ShelfRequests().Should().HaveCount(2));
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));
        page.FindAll(".dialog").Should().BeEmpty();
        save.Release();

        page.WaitForAssertion(() => page.Find(".alert--success").TextContent.Should().Be("Dune güncellendi."));
        page.FindAll(".item-card")[0].QuerySelector(".shelf-card__rating")!.TextContent.Should().EndWith("★ 10");
        Titles(page).Should().Equal("Dune", "Hades");
    }

    [Fact]
    public void An_api_error_shows_the_turkish_message_and_a_retry()
    {
        Api.OnProblem(HttpMethod.Get, ShelfPath, HttpStatusCode.TooManyRequests, "rate_limited");

        var page = Render<MyShelf>();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Çok fazla deneme yaptın"));
        page.FindAll("button").Should().Contain(b => HasText(b, "Tekrar dene"));
        page.FindAll(".item-card").Should().BeEmpty();
    }

    [Fact]
    public void A_timeout_shows_the_network_message_and_a_retry_that_recovers()
    {
        var network = UseSlowNetwork();
        network.Fail(HttpMethod.Get, ShelfPath, new TaskCanceledException("HttpClient.Timeout elapsed.", new TimeoutException()));
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Dune()));

        var page = Render<MyShelf>();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Sunucuya ulaşılamadı"));
        network.Recover(HttpMethod.Get, ShelfPath);
        page.FindAll("button").Single(b => HasText(b, "Tekrar dene")).Click();

        page.WaitForAssertion(() => Titles(page).Should().Equal("Dune"));
        page.FindAll(".alert--error").Should().BeEmpty();
    }

    [Fact]
    public async Task An_expired_session_refreshes_the_session_instead_of_offering_a_retry()
    {
        SignInAs("gece_okuru");
        var session = Services.GetRequiredService<AuthSession>();
        (await session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeTrue();

        // The cookie expires while the page is open.
        Api.OnProblem(HttpMethod.Get, "/api/v1/me", HttpStatusCode.Unauthorized, "unauthorized");
        Api.OnProblem(HttpMethod.Get, ShelfPath, HttpStatusCode.Unauthorized, "unauthorized");
        var page = Render<MyShelf>();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Devam etmek için giriş yap."));
        page.WaitForAssertion(() => session.Me.Should().BeNull());
        (await session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse("the router then sends the user to the login page");
        page.FindAll("button").Should().NotContain(b => HasText(b, "Tekrar dene"));
    }

    [Fact]
    public void A_next_page_that_cannot_be_retried_offers_to_reload_the_list()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page("page-2", 2, Dune()));
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));

        Api.OnProblem(HttpMethod.Get, ShelfPath, HttpStatusCode.BadRequest, "invalid_cursor");
        page.FindAll("button").Single(b => HasText(b, "Daha fazla")).Click();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Liste değişti"));
        page.FindAll("button").Should().NotContain(b => HasText(b, "Daha fazla") || HasText(b, "Tekrar dene"));
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));
        page.FindAll("button").Single(b => HasText(b, "Listeyi yenile")).Click();

        page.WaitForAssertion(() => Titles(page).Should().Equal("Dune", "Hades"));
        page.FindAll(".alert--error").Should().BeEmpty();
        ShelfRequests()[^1].Should().NotContain("Cursor=");
    }

    private static bool HasText(AngleSharp.Dom.IElement element, string text) => string.Equals(element.TextContent, text, StringComparison.Ordinal);

    // A held answer renders nothing for WaitForAssertion to wake up on, so wait for the network itself.
    private static Task ArrivedAsync(Task step) => step.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

    // Lets a held answer through while the page is busy, so the client reads it in full before the click on the
    // chip starts the newer load. The old load's continuation queues up behind that click and finds its answer
    // complete: nothing cancelled it in flight, only the page's own check can drop it.
    private static Task DeliverAsTheNextFilterStartsAsync(IRenderedComponent<MyShelf> page, HeldAnswer held, string chip) =>
        page.InvokeAsync(() =>
        {
            held.Release();
            SpinWait.SpinUntil(() => held.Read.IsCompleted, TimeSpan.FromSeconds(5)).Should().BeTrue("the client reads the answer");
            Thread.Sleep(TimeSpan.FromMilliseconds(100)); // The client parses it and posts the page's continuation.
            page.FindAll(".chips button").Single(b => HasText(b, chip)).Click();
        });

    private static List<string> Titles(IRenderedComponent<MyShelf> page) => [.. page.FindAll(".item-card__title").Select(t => t.TextContent)];

    private static void RemoveFirstCard(IRenderedComponent<MyShelf> page)
    {
        page.FindAll(".shelf-card__edit")[0].Click();
        page.WaitForAssertion(() => page.FindAll(".dialog button").Should().Contain(b => HasText(b, "Raftan kaldır")));
        page.FindAll(".dialog button").Single(b => HasText(b, "Raftan kaldır")).Click();
    }

    // Puts a SlowNetwork in front of FakeApi for everything that uses the generated client (page, session, catalog).
    // Call it before the first service is resolved.
    private SlowNetwork UseSlowNetwork()
    {
        var network = new SlowNetwork(Api);
        var http = new HttpClient(network) { BaseAddress = new Uri("https://kunai.test/") };
        Services.AddSingleton(http);
        Services.AddSingleton(new KunaiApiClient(new DefaultRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http) { BaseUrl = "https://kunai.test" }));
        return network;
    }

    private List<string> ShelfRequests() =>
        [.. Api.Requests.Where(r => r.Method == HttpMethod.Get && r.PathAndQuery.StartsWith(ShelfPath, StringComparison.Ordinal)).Select(r => r.PathAndQuery)];

    private static object Page(string? nextCursor, int totalCount, params object[] items) => new { items, nextCursor, totalCount };

    private static object Dune() => Entry(DuneId, "book", "Dune", "inProgress", rating: 8, progress: 120, progressTotal: 412, favorite: true);

    private static object Hades() => Entry(HadesId, "game", "Hades", "completed", rating: null, progress: 100, progressTotal: null, favorite: false);

    // The JSON the API sends: camelCase names, enum strings, dates as yyyy-MM-dd.
    private static object Entry(Guid id, string type, string title, string status, int? rating, int? progress, int? progressTotal, bool favorite) => new
    {
        item = new { id, type, title, year = 2020, coverUrl = (string?)null, creators = new[] { "Yazar" }, progressTotal },
        status,
        rating,
        startedOn = "2026-11-01",
        finishedOn = string.Equals(status, "completed", StringComparison.Ordinal) ? "2026-11-20" : null,
        progress,
        review = (string?)null,
        isFavorite = favorite,
        createdAt = DateTimeOffset.Parse("2026-11-01T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        updatedAt = DateTimeOffset.Parse("2026-11-20T18:30:00Z", System.Globalization.CultureInfo.InvariantCulture),
    };
}
