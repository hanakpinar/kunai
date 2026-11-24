using System.Net;
using System.Text.Json;

using Bunit;

using Kunai.Web.Client.Infrastructure;
using Kunai.Web.Client.Pages.Settings;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Settings;

public sealed class SettingsPageTests : KunaiTestContext
{
    private const string Me = "/api/v1/me";

    [Fact]
    public void The_account_from_the_api_fills_every_section()
    {
        Api.On(HttpMethod.Get, Me, Account(isProfilePublic: false, locale: "en", timeZone: "Asia/Kolkata", avatarId: "ninja-07"));
        var page = Render<SettingsPage>();

        page.WaitForAssertion(() => page.Find("input[autocomplete=nickname]").GetAttribute("value").Should().Be("Gece Okuru"));
        page.Find("#settings-bio-counter").TextContent.Should().Contain("19/300");
        page.Find("input[aria-describedby=settings-visibility-hint]").HasAttribute("checked").Should().BeFalse();
        var selects = page.FindAll("section[aria-labelledby=settings-preferences] select");
        selects[0].GetAttribute("value").Should().Be("en");
        selects[1].GetAttribute("value").Should().Be("Asia/Kolkata", "a zone outside the common list stays selectable");
        page.Find("input[value=ninja-07]").HasAttribute("checked").Should().BeTrue();
        page.Find(".avatar-picker__option--selected img").GetAttribute("src").Should().Be("api/v1/avatars/ninja-07.svg");
        page.FindAll(".avatar-picker img").Should().HaveCount(12).And.OnlyContain(img => !string.IsNullOrEmpty(img.GetAttribute("alt")));
        page.Find("section[aria-labelledby=settings-username] a").GetAttribute("href").Should().Be("u/gece_okuru");
    }

    [Fact]
    public void Saving_preferences_sends_all_four_fields_and_refreshes_the_session()
    {
        Api.On(HttpMethod.Get, Me, Account(isProfilePublic: false));
        Api.On(HttpMethod.Put, "/api/v1/me/settings", Account(isProfilePublic: true, timeZone: "Europe/Berlin", avatarId: "ninja-11"));
        var page = Render<SettingsPage>();

        page.WaitForElement("input[value=ninja-11]").Change("ninja-11");
        page.Find("input[aria-describedby=settings-visibility-hint]").Change(true);
        page.FindAll("section[aria-labelledby=settings-preferences] select")[1].Change("Europe/Berlin");
        page.Find("section[aria-labelledby=settings-preferences] form").Submit();

        page.WaitForAssertion(() => page.Find("section[aria-labelledby=settings-preferences] .alert--success").TextContent.Should().Contain("kaydedildi"));
        using var sent = JsonDocument.Parse(Api.BodiesSentTo(HttpMethod.Put, "/api/v1/me/settings").Single()!);
        sent.RootElement.GetProperty("isProfilePublic").GetBoolean().Should().BeTrue();
        sent.RootElement.GetProperty("locale").GetString().Should().Be("tr");
        sent.RootElement.GetProperty("timeZone").GetString().Should().Be("Europe/Berlin");
        sent.RootElement.GetProperty("avatarId").GetString().Should().Be("ninja-11");
        Api.BodiesSentTo(HttpMethod.Get, Me).Count().Should().Be(2, "the page loads once and the session refreshes once");
    }

    [Fact]
    public void A_username_change_inside_the_cooldown_shows_the_turkish_message()
    {
        Api.On(HttpMethod.Get, Me, Account());
        Api.OnProblem(HttpMethod.Put, "/api/v1/me/username", HttpStatusCode.UnprocessableEntity, "username_change_cooldown");
        var page = Render<SettingsPage>();

        page.WaitForElement("section[aria-labelledby=settings-username] input").Change("  Yeni_Ninja ");
        page.Find("section[aria-labelledby=settings-username] form").Submit();

        page.WaitForAssertion(() => page.Find("section[aria-labelledby=settings-username] .alert--error").TextContent.Should().Contain("30 günde bir"));
        Api.BodiesSentTo(HttpMethod.Put, "/api/v1/me/username").Single().Should().Contain("\"username\":\"yeni_ninja\"");
        page.FindAll(".alert--success").Should().BeEmpty();
    }

    [Fact]
    public void A_wrong_current_password_is_shown_next_to_its_field()
    {
        Api.On(HttpMethod.Get, Me, Account());
        Api.OnProblem(
            HttpMethod.Post,
            "/api/v1/me/password",
            HttpStatusCode.BadRequest,
            "validation_failed",
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["currentPassword"] = ["password_mismatch"] });
        var page = Render<SettingsPage>();

        const string Passwords = "section[aria-labelledby=settings-password] input[type=password]";
        page.WaitForElements(Passwords)[0].Change("not my password");
        page.FindAll(Passwords)[1].Change("a long new passphrase");
        page.FindAll(Passwords)[2].Change("a long new passphrase");
        page.Find("section[aria-labelledby=settings-password] form").Submit();

        page.WaitForAssertion(() => page.Find("section[aria-labelledby=settings-password] .validation-message").TextContent.Should().Be("Şifre yanlış."));
        Api.BodiesSentTo(HttpMethod.Post, "/api/v1/me/password").Single().Should().Contain("\"currentPassword\":\"not my password\"");
    }

    [Fact]
    public void Deleting_the_account_needs_the_checkbox_then_signs_out_and_goes_home()
    {
        Api.On(HttpMethod.Get, Me, Account());
        Api.On(HttpMethod.Delete, Me, null, HttpStatusCode.NoContent);
        var page = Render<SettingsPage>();
        var delete = page.WaitForElement("section[aria-labelledby=settings-delete] button[type=submit]");
        delete.HasAttribute("disabled").Should().BeTrue("the user has not confirmed yet");

        page.Find("section[aria-labelledby=settings-delete] input[type=password]").Change("my password!!");
        page.Find("section[aria-labelledby=settings-delete] input[type=checkbox]").Change(true);
        page.Find("section[aria-labelledby=settings-delete] button[type=submit]").HasAttribute("disabled").Should().BeFalse();
        Api.OnProblem(HttpMethod.Get, Me, HttpStatusCode.Unauthorized, "unauthorized");
        page.Find("section[aria-labelledby=settings-delete] form").Submit();

        page.WaitForAssertion(() => Services.GetRequiredService<NavigationManager>().Uri.Should().Be("http://localhost/"));
        Api.BodiesSentTo(HttpMethod.Delete, Me).Single().Should().Contain("\"password\":\"my password!!\"");
        Services.GetRequiredService<AuthSession>().Me.Should().BeNull();
    }

    /// <summary>GET /me as the API sends it: camelCase, every field present.</summary>
    private static object Account(bool isProfilePublic = true, string locale = "tr", string timeZone = "Europe/Istanbul", string avatarId = "ninja-03") => new
    {
        id = Guid.Parse("6f1c2a9e-1d4b-4c7a-9a51-2f0e8d3b7c11"),
        username = "gece_okuru",
        email = "gece@test.kunai",
        displayName = "Gece Okuru",
        bio = "Her gece bir bölüm.",
        avatarId,
        isProfilePublic,
        locale,
        timeZone,
        createdAt = "2026-01-15T09:30:00Z",
    };
}
