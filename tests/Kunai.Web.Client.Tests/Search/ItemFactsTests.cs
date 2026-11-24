using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Pages.Search.Items;

namespace Kunai.Web.Client.Tests.Search;

public sealed class ItemFactsTests
{
    [Theory]
    [InlineData("TV", "TV dizisi")]
    [InlineData("TV_SHORT", "Kısa dizi")]
    [InlineData("MOVIE", "Film")]
    [InlineData("SPECIAL", "Özel bölüm")]
    [InlineData("ONE_SHOT", "Tek bölümlük")]
    [InlineData("NOVEL", "Light novel")]
    [InlineData("MANGA", "Manga")]
    [InlineData("SOMETHING_NEW", "SOMETHING_NEW")]
    public void AniList_formats_read_in_turkish_and_unknown_ones_as_sent(string format, string expected)
    {
        var anime = new ItemResponse { Type = ItemType.Anime, ProgressTotal = 12, Details = new ItemDetails { Format = format, RuntimeMinutes = 24 } };

        ItemFacts.For(anime).Should().Equal(
            new ItemFact("Bölüm sayısı", "12"),
            new ItemFact("Bölüm süresi", "24 dk"),
            new ItemFact("Format", expected));
    }

    [Fact]
    public void Only_books_series_anime_and_manga_count_progress_against_a_total()
    {
        Enum.GetValues<ItemType>().Where(type => ItemFacts.HasProgressTotal(type))
            .Should().BeEquivalentTo([ItemType.Book, ItemType.Series, ItemType.Anime, ItemType.Manga]);
    }
}
