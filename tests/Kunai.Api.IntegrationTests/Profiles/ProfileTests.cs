using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.Features.Catalog;
using Kunai.Api.IntegrationTests.Identity;
using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Profiles;

public sealed class ProfileTests(KunaiApiFactory factory)
{
    private static readonly int ThisYear = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).Year;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Public_profile_shows_counts_this_year_favorites_and_the_belt()
    {
        var user = await factory.RegisterConfirmedAsync();
        var client = await factory.CreateSignedInClientAsync(user);

        // 9 completed books x 3 points = 27 points: yellow belt (25+).
        for (var i = 0; i < 9; i++)
        {
            var bookId = await factory.CreateItemAsync(ItemType.Book, progressTotal: 100);
            await client.PutAsJsonAsync($"/api/v1/me/shelf/{bookId}", new { status = "completed", finishedOn = $"{ThisYear}-03-0{(i % 9) + 1}", isFavorite = i < 2 }, Ct);
        }

        var movieId = await factory.CreateItemAsync(ItemType.Movie);
        await client.PutAsJsonAsync($"/api/v1/me/shelf/{movieId}", new { status = "planned" }, Ct);

        var profile = await factory.CreateApiClient().GetFromJsonAsync<JsonElement>($"/api/v1/users/{user.Username}", Ct);

        profile.GetProperty("rank").GetProperty("code").GetString().Should().Be("yellow");
        profile.GetProperty("rank").GetProperty("points").GetInt32().Should().Be(27);
        profile.GetProperty("rank").GetProperty("nextCode").GetString().Should().Be("orange");
        profile.GetProperty("avatarUrl").GetString().Should().MatchRegex(@"^/api/v1/avatars/ninja-\d\d\.svg\?belt=yellow$");
        var books = profile.GetProperty("counts").EnumerateArray().Single(c => string.Equals(c.GetProperty("type").GetString(), "book", StringComparison.Ordinal));
        books.GetProperty("completed").GetInt32().Should().Be(9);
        var movies = profile.GetProperty("counts").EnumerateArray().Single(c => string.Equals(c.GetProperty("type").GetString(), "movie", StringComparison.Ordinal));
        movies.GetProperty("planned").GetInt32().Should().Be(1);
        profile.GetProperty("thisYear").GetProperty("year").GetInt32().Should().Be(ThisYear);
        profile.GetProperty("thisYear").GetProperty("completed")[0].GetProperty("count").GetInt32().Should().Be(9);
        profile.GetProperty("favorites").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Profile_reflects_shelf_changes_immediately()
    {
        var user = await factory.RegisterConfirmedAsync();
        var client = await factory.CreateSignedInClientAsync(user);
        var anonymous = factory.CreateApiClient();
        (await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/users/{user.Username}", Ct)).GetProperty("rank").GetProperty("points").GetInt32().Should().Be(0);

        var gameId = await factory.CreateItemAsync(ItemType.Game);
        await client.PutAsJsonAsync($"/api/v1/me/shelf/{gameId}", new { status = "completed" }, Ct);

        (await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/users/{user.Username}", Ct)).GetProperty("rank").GetProperty("points").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task Private_profiles_are_404_for_everyone_but_their_owner()
    {
        var user = await factory.RegisterConfirmedAsync();
        var owner = await factory.CreateSignedInClientAsync(user);
        var settings = await owner.PutAsJsonAsync("/api/v1/me/settings", new { isProfilePublic = false, locale = "tr", timeZone = "Europe/Istanbul", avatarId = "ninja-05" }, Ct);
        settings.StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInTests.ShouldBeProblem(await factory.CreateApiClient().GetAsync($"/api/v1/users/{user.Username}", Ct), HttpStatusCode.NotFound, "user_not_found");
        var stranger = await factory.CreateSignedInClientAsync();
        (await stranger.GetAsync($"/api/v1/users/{user.Username}/shelf", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"/api/v1/users/{user.Username}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Settings_and_profile_updates_are_validated_and_saved()
    {
        var client = await factory.CreateSignedInClientAsync();

        var badZone = await client.PutAsJsonAsync("/api/v1/me/settings", new { isProfilePublic = true, locale = "tr", timeZone = "Mars/Olympus", avatarId = "ninja-01" }, Ct);
        (await SignInTests.ShouldBeProblem(badZone, HttpStatusCode.BadRequest, "validation_failed")).GetProperty("errors").GetProperty("timeZone")[0].GetString().Should().Be("time_zone_invalid");

        var badAvatar = await client.PutAsJsonAsync("/api/v1/me/settings", new { isProfilePublic = true, locale = "tr", timeZone = "Europe/Istanbul", avatarId = "ninja-99" }, Ct);
        (await SignInTests.ShouldBeProblem(badAvatar, HttpStatusCode.BadRequest, "validation_failed")).GetProperty("errors").GetProperty("avatarId")[0].GetString().Should().Be("avatar_invalid");

        var settings = await client.PutAsJsonAsync("/api/v1/me/settings", new { isProfilePublic = true, locale = "en", timeZone = "America/New_York", avatarId = "ninja-12" }, Ct);
        var me = await settings.Content.ReadFromJsonAsync<JsonElement>(Ct);
        me.GetProperty("locale").GetString().Should().Be("en");
        me.GetProperty("timeZone").GetString().Should().Be("America/New_York");
        me.GetProperty("avatarId").GetString().Should().Be("ninja-12");

        var profile = await client.PutAsJsonAsync("/api/v1/me/profile", new { displayName = "Kunai Ustası", bio = "Bilim kurgu sever." }, Ct);
        (await profile.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("displayName").GetString().Should().Be("Kunai Ustası");
    }

    [Fact]
    public async Task Username_can_change_once_per_30_days_and_never_to_a_taken_one()
    {
        var taken = await factory.RegisterConfirmedAsync();
        var client = await factory.CreateSignedInClientAsync();

        await SignInTests.ShouldBeProblem(await client.PutAsJsonAsync("/api/v1/me/username", new { username = taken.Username }, Ct), HttpStatusCode.Conflict, "username_taken");

        var fresh = TestData.NewUsername();
        var changed = await client.PutAsJsonAsync("/api/v1/me/username", new { username = fresh }, Ct);
        (await changed.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("username").GetString().Should().Be(fresh);

        var again = await client.PutAsJsonAsync("/api/v1/me/username", new { username = TestData.NewUsername() }, Ct);
        await SignInTests.ShouldBeProblem(again, HttpStatusCode.UnprocessableEntity, "username_change_cooldown");
    }

    [Fact]
    public async Task Stats_count_completions_by_month_with_pages_and_average_rating()
    {
        var user = await factory.RegisterConfirmedAsync();
        var client = await factory.CreateSignedInClientAsync(user);
        await Complete(client, ItemType.Book, 300, "2025-01-15", 8);
        await Complete(client, ItemType.Book, 200, "2025-01-20", 6);
        await Complete(client, ItemType.Series, 10, "2025-07-01", null);
        await Complete(client, ItemType.Book, 999, "2024-12-31", 10);

        var stats = await factory.CreateApiClient().GetFromJsonAsync<JsonElement>($"/api/v1/users/{user.Username}/stats?year=2025", Ct);

        stats.GetProperty("pagesRead").GetInt32().Should().Be(500);
        stats.GetProperty("episodesWatched").GetInt32().Should().Be(10);
        stats.GetProperty("completedByMonth")[0].GetInt32().Should().Be(2);
        stats.GetProperty("completedByMonth")[6].GetInt32().Should().Be(1);
        var books = stats.GetProperty("types").EnumerateArray().Single(t => string.Equals(t.GetProperty("type").GetString(), "book", StringComparison.Ordinal));
        books.GetProperty("averageRating").GetDouble().Should().Be(7.0);
        stats.GetProperty("availableYears").EnumerateArray().Select(y => y.GetInt32()).Should().Equal(2025, 2024);
    }

    [Fact]
    public async Task Goals_track_progress_toward_yearly_targets()
    {
        var user = await factory.RegisterConfirmedAsync();
        var client = await factory.CreateSignedInClientAsync(user);
        await Complete(client, ItemType.Book, null, "2025-02-01", null);

        var put = await client.PutAsJsonAsync("/api/v1/me/goals/2025", new { targets = new object[] { new { type = "book", target = 4 }, new { type = "movie", target = 10 } } }, Ct);
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var goals = await factory.CreateApiClient().GetFromJsonAsync<JsonElement>($"/api/v1/users/{user.Username}/goals/2025", Ct);
        var book = goals.GetProperty("goals").EnumerateArray().Single(g => string.Equals(g.GetProperty("type").GetString(), "book", StringComparison.Ordinal));
        book.GetProperty("completed").GetInt32().Should().Be(1);
        book.GetProperty("percent").GetInt32().Should().Be(25);

        var duplicate = await client.PutAsJsonAsync("/api/v1/me/goals/2025", new { targets = new object[] { new { type = "book", target = 1 }, new { type = "book", target = 2 } } }, Ct);
        await SignInTests.ShouldBeProblem(duplicate, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Ranks_and_avatars_are_public()
    {
        var client = factory.CreateApiClient();

        var ranks = await client.GetFromJsonAsync<JsonElement>("/api/v1/ranks", Ct);
        ranks.EnumerateArray().Select(r => r.GetProperty("code").GetString()).Should().Equal("white", "yellow", "orange", "green", "blue", "brown", "black");

        var avatar = await client.GetAsync("/api/v1/avatars/ninja-03.svg?belt=green", Ct);
        avatar.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        (await avatar.Content.ReadAsStringAsync(Ct)).Should().StartWith("<svg").And.Contain("#2e9e4f");
        (await client.GetAsync("/api/v1/avatars/ninja-13.svg", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task Complete(HttpClient client, ItemType type, int? total, string finishedOn, int? rating)
    {
        var itemId = await factory.CreateItemAsync(type, total);
        var response = await client.PutAsJsonAsync($"/api/v1/me/shelf/{itemId}", new { status = "completed", finishedOn, rating }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
