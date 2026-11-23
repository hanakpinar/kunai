using Kunai.Api.Features.Catalog;

namespace Kunai.Api.UnitTests.Catalog;

public sealed class ExternalRefTests
{
    [Theory]
    [InlineData("tmdb:movie:603", ItemSource.Tmdb, "movie", "603")]
    [InlineData("tmdb:tv:1399", ItemSource.Tmdb, "tv", "1399")]
    [InlineData("gbooks:volume:zyTCAlFPjgYC", ItemSource.GoogleBooks, "volume", "zyTCAlFPjgYC")]
    [InlineData("olib:work:OL45883W", ItemSource.OpenLibrary, "work", "OL45883W")]
    [InlineData("igdb:game:1942", ItemSource.Igdb, "game", "1942")]
    [InlineData("anilist:manga:30013", ItemSource.AniList, "manga", "30013")]
    public void Parses_and_round_trips(string value, ItemSource source, string kind, string id)
    {
        ExternalRef.TryParse(value, out var parsed).Should().BeTrue();

        parsed.Should().Be(new ExternalRef(source, kind, id));
        parsed!.Value.ToString().Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tmdb:603")]
    [InlineData("netflix:movie:1")]
    [InlineData("tmdb::603")]
    [InlineData("tmdb:movie:1:extra")]
    public void Rejects_malformed_references(string? value) =>
        ExternalRef.TryParse(value, out _).Should().BeFalse();
}
