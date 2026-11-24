using System.Text.Json;
using System.Text.Json.Nodes;

using Bunit;

using Kunai.Web.Client.Pages.Search.Items;
using Kunai.Web.Client.Tests.Infrastructure;

namespace Kunai.Web.Client.Tests.Search;

public sealed class ItemDetailTests : KunaiTestContext
{
    private static readonly Guid BookId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-000000000160");
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
}
