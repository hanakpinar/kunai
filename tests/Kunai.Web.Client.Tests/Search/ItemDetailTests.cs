using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using Bunit;
using Bunit.Extensions.WaitForHelpers;

using Kunai.Web.Client.Api;
using Kunai.Web.Client.Pages.Search.Items;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Bundle;

namespace Kunai.Web.Client.Tests.Search;

public sealed class ItemDetailTests : KunaiTestContext
{
    private static readonly Guid BookId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-000000000160");
    private static readonly Guid MovieId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-3b2c4d5e6f70");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Shows_the_details_the_provider_attribution_and_an_add_button_when_not_on_the_shelf()
    {
        SignInAs("ninja");
        var catalog = JsonSerializer.SerializeToNode(TestCatalog.ItemTypes, Json)!;
        catalog["sources"] = new JsonArray(
            JsonSerializer.SerializeToNode(new { source = "tmdb", name = "TMDB", url = "https://www.themoviedb.org", notice = "This product uses the TMDB API." }),
            JsonSerializer.SerializeToNode(new { source = "openLibrary", name = "Open Library", url = "https://openlibrary.org", notice = "Book data from Open Library" }));
        Api.On(HttpMethod.Get, "/api/v1/item-types", catalog);
        Api.On(HttpMethod.Get, $"/api/v1/items/{BookId}", ItemJson.Book(BookId));
        // GET /me/shelf/{id} is not registered: the fake answers 404, which means "not on the shelf".

        var page = Render<ItemDetail>(parameters => parameters.Add(p => p.Id, BookId));

        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Kürk Mantolu Madonna"));
        page.FindAll(".item-detail__original").Should().BeEmpty("the original title equals the title");
        page.Find(".detail__meta").TextContent.Should().Contain("1943").And.Contain("Sabahattin Ali");
        page.Find(".detail__description").TextContent.Should().Be("Raif Efendi'nin defteri.\nBerlin'de bir sergi.");
        page.Find(".item-detail__facts").TextContent.Should().Contain("Sayfa sayısı").And.Contain("160").And.Contain("9789753638029").And.Contain("Yapı Kredi Yayınları");
        page.Find(".item-detail__attribution a").GetAttribute("href").Should().Be("https://openlibrary.org");
        page.Find(".item-detail__attribution").TextContent.Should().Contain("Book data from Open Library");

        page.WaitForAssertion(() => page.Find(".shelf-panel button").TextContent.Should().Be("Rafa ekle"));
        page.Find(".shelf-panel button").Click();
        page.WaitForAssertion(() => page.Find("section.dialog h2").TextContent.Should().Be("Rafa ekle"));
        Api.Requests.Should().Contain(r => r.Method == HttpMethod.Get && r.PathAndQuery == $"/api/v1/me/shelf/{BookId}");
    }

    [Fact]
    public void An_unknown_id_shows_the_not_found_message_with_a_way_home()
    {
        Api.OnProblem(HttpMethod.Get, $"/api/v1/items/{BookId}", HttpStatusCode.NotFound, "item_not_found");

        var page = Render<ItemDetail>(parameters => parameters.Add(p => p.Id, BookId));

        page.WaitForAssertion(() => page.Find(".empty-state").TextContent.Should().Contain("Bu içerik bulunamadı"));
        page.Find(".empty-state a[href='']").TextContent.Should().Be("Ana sayfaya dön");
        page.FindAll(".alert--error").Should().BeEmpty();
    }

    [Fact]
    public void An_entry_on_the_shelf_shows_its_status_and_removing_it_offers_to_add_again()
    {
        SignInAs("ninja");
        Api.AnswerAsynchronously = true;
        Api.On(HttpMethod.Get, $"/api/v1/items/{BookId}", ItemJson.Book(BookId));
        Api.On(HttpMethod.Get, $"/api/v1/me/shelf/{BookId}", new
        {
            item = new { id = BookId, type = "book", title = "Kürk Mantolu Madonna", year = 1943, coverUrl = (string?)null, creators = new[] { "Sabahattin Ali" }, progressTotal = 160 },
            status = "inProgress",
            rating = 9,
            startedOn = "2026-11-01",
            finishedOn = (string?)null,
            progress = 40,
            review = (string?)null,
            isFavorite = true,
            createdAt = DateTimeOffset.UtcNow,
            updatedAt = DateTimeOffset.UtcNow,
        });
        Api.On(HttpMethod.Delete, $"/api/v1/me/shelf/{BookId}", null, HttpStatusCode.NoContent);

        var page = Render<ItemDetail>(parameters => parameters.Add(p => p.Id, BookId));

        page.WaitForAssertion(() => page.Find(".shelf-panel .status-badge").TextContent.Should().Be("Okuyorum"));
        page.WaitForAssertion(() => page.Find(".shelf-panel__state").TextContent.Should().Contain("40 / 160 sayfa"));
        page.Find(".shelf-panel__state").TextContent.Should().Contain("9").And.Contain("Favorin");
        page.Find(".shelf-panel button").TextContent.Should().Be("Düzenle");

        page.Find(".shelf-panel button").Click();
        page.WaitForAssertion(() => page.Find("section.dialog h2").TextContent.Should().Be("Rafını düzenle"));
        page.FindAll("section.dialog button").Single(b => string.Equals(b.TextContent, "Raftan kaldır", StringComparison.Ordinal)).Click();

        page.WaitForAssertion(() => page.Find(".shelf-panel button").TextContent.Should().Be("Rafa ekle"));
        page.FindAll(".shelf-panel .status-badge").Should().BeEmpty();
        page.FindAll("section.dialog").Should().BeEmpty();
        Api.Requests.Should().ContainSingle(r => r.Method == HttpMethod.Delete && r.PathAndQuery == $"/api/v1/me/shelf/{BookId}");
    }

    [Fact]
    public void An_older_item_that_answers_last_does_not_replace_the_one_now_shown()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        UseApiThat(holdsBack: $"/api/v1/items/{BookId}", until: release.Task);
        Api.On(HttpMethod.Get, $"/api/v1/items/{BookId}", ItemJson.Book(BookId));
        Api.On(HttpMethod.Get, $"/api/v1/items/{MovieId}", ItemJson.Movie(MovieId));

        // Back/forward between two items reuses the page with a new id while the first request is still running.
        var page = Render<ItemDetail>(parameters => parameters.Add(p => p.Id, BookId));
        page.Render(parameters => parameters.Add(p => p.Id, MovieId));
        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Dune"));

        release.SetResult();

        var replaced = () => page.WaitForState(() => !string.Equals(page.Find("h1").TextContent, "Dune", StringComparison.Ordinal), TimeSpan.FromMilliseconds(300));
        replaced.Should().Throw<WaitForFailedException>("the book's late answer belongs to the page it left");
        page.Find(".item-detail__type").TextContent.Should().Be("Film");
    }

    [Fact]
    public void A_book_just_added_by_hand_is_greeted_once_and_the_flag_leaves_the_address()
    {
        SignInAs("ninja");
        Api.On(HttpMethod.Get, $"/api/v1/items/{BookId}", ItemJson.Book(BookId, catalogStatus: "pendingReview", source: "manual"));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"items/{BookId}?added=true");

        var page = Render<ItemDetail>(parameters => parameters.Add(p => p.Id, BookId));

        page.WaitForAssertion(() => navigation.Uri.Should().Be($"http://localhost/items/{BookId}"));
        page.WaitForAssertion(() => page.Find(".alert--success").TextContent.Should().Contain("Kitabı ekledin!").And.Contain("moderatör onayı bekliyor"));
        page.Find(".item-detail__attribution").TextContent.Should().Contain("elle eklendi");
        Api.Requests.Count(r => string.Equals(r.PathAndQuery, $"/api/v1/items/{BookId}", StringComparison.Ordinal)).Should().Be(1, "dropping the flag does not load the item again");
    }

    [Fact]
    public void A_failure_a_retry_cannot_fix_shows_no_retry_button()
    {
        Api.OnProblem(HttpMethod.Get, $"/api/v1/items/{BookId}", HttpStatusCode.BadRequest, "bad_request");

        var page = Render<ItemDetail>(parameters => parameters.Add(p => p.Id, BookId));

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("İstek anlaşılamadı"));
        page.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void A_server_failure_can_be_retried()
    {
        Api.OnProblem(HttpMethod.Get, $"/api/v1/items/{BookId}", HttpStatusCode.InternalServerError, "internal_error");
        var page = Render<ItemDetail>(parameters => parameters.Add(p => p.Id, BookId));
        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Sunucuda bir hata oluştu"));

        Api.On(HttpMethod.Get, $"/api/v1/items/{BookId}", ItemJson.Book(BookId));
        page.Find(".item-retry button").Click();

        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Kürk Mantolu Madonna"));
    }

    /// <summary>Puts a handler in front of the fake API that holds back answers to one path.</summary>
    private void UseApiThat(string holdsBack, Task until)
    {
        var http = new HttpClient(new HoldBack(holdsBack, until) { InnerHandler = Api }) { BaseAddress = new Uri("https://kunai.test/") };
        Services.AddSingleton(new KunaiApiClient(new DefaultRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http) { BaseUrl = "https://kunai.test" }));
    }

    /// <summary>Answers <paramref name="path"/> only after <paramref name="release"/>, like a slow response already on its way.</summary>
    private sealed class HoldBack(string path, Task release) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (string.Equals(request.RequestUri?.AbsolutePath, path, StringComparison.Ordinal))
            {
                await release;
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
