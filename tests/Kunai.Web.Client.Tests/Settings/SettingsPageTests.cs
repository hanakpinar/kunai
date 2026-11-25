using System.Net;
using System.Text.Json;

using Bunit;
using Bunit.TestDoubles;

using Kunai.Web.Client.Infrastructure;
using Kunai.Web.Client.Pages.Settings;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Settings;

public sealed class SettingsPageTests : KunaiTestContext
{
    private const string Me = "/api/v1/me";
    private const string SettingsPath = "/api/v1/me/settings";
    private const string ProfilePath = "/api/v1/me/profile";
    private const string PasswordPath = "/api/v1/me/password";
    private const string LogoutPath = "/api/v1/auth/logout";
    private const string Preferences = "section[aria-labelledby=settings-preferences]";
    private const string SignInRedirect = "http://localhost/login?returnUrl=settings";

    public SettingsPageTests()
    {
        // bUnit puts a placeholder in front of AddAuthorizationCore's service; the real router needs the real one.
        Services.AddSingleton<IAuthorizationService, DefaultAuthorizationService>();
    }

    [Fact]
    public void The_account_from_the_api_fills_every_section()
    {
        Api.On(HttpMethod.Get, Me, Account(isProfilePublic: false, locale: "en", timeZone: "Asia/Kolkata", avatarId: "ninja-07"));
        var page = Render<SettingsPage>();

        page.WaitForAssertion(() => page.Find("input[autocomplete=nickname]").GetAttribute("value").Should().Be("Gece Okuru"));
        page.Find("#settings-bio-counter").TextContent.Should().Contain("19/300");
        page.Find("input[aria-describedby=settings-visibility-hint]").HasAttribute("checked").Should().BeFalse("the form starts public, so this shows the account was read");
        var selects = page.FindAll($"{Preferences} select");
        selects[0].GetAttribute("value").Should().Be("en");
        selects[1].GetAttribute("value").Should().Be("Asia/Kolkata", "a zone outside the common list stays selectable");
        page.Find("input[value=ninja-07]").HasAttribute("checked").Should().BeTrue();
        page.Find(".avatar-picker__option--selected img").GetAttribute("src").Should().Be("api/v1/avatars/ninja-07.svg");
        page.FindAll(".avatar-picker img").Should().HaveCount(12).And.OnlyContain(img => !string.IsNullOrEmpty(img.GetAttribute("alt")));
        page.Find("section[aria-labelledby=settings-username] a").GetAttribute("href").Should().Be("u/gece_okuru");
    }

    [Theory]
    [InlineData("settings-tz")]
    [InlineData("settings-new-password")]
    [InlineData("settings-new-username")]
    public void A_field_hint_describes_its_control_instead_of_joining_its_name(string id)
    {
        Api.On(HttpMethod.Get, Me, Account());
        var page = Render<SettingsPage>();

        var control = page.WaitForElement($"#{id}");
        page.Find($"label[for={id}]").TextContent.Should().NotBeNullOrWhiteSpace();
        control.Closest("label").Should().BeNull("the label points at the control instead of wrapping it");
        control.GetAttribute("aria-describedby").Should().Be($"{id}-hint");
        page.Find($"#{id}-hint").Closest("label").Should().BeNull("text inside the label would be read as the field's name");
    }

    [Fact]
    public void Saving_preferences_sends_all_four_fields_and_refreshes_the_session()
    {
        Api.On(HttpMethod.Get, Me, Account(isProfilePublic: false));
        Api.On(HttpMethod.Put, SettingsPath, Account(isProfilePublic: true, timeZone: "Europe/Berlin", avatarId: "ninja-11"));
        var page = Render<SettingsPage>();

        const string Status = $"{Preferences} p[role=status]";
        page.WaitForElement(Status).TextContent.Should().BeEmpty("the live region is on the page before there is anything to announce");
        page.Find("input[value=ninja-11]").Change("ninja-11");
        page.Find("input[aria-describedby=settings-visibility-hint]").Change(true);
        page.FindAll($"{Preferences} select")[1].Change("Europe/Berlin");
        page.Find($"{Preferences} form").Submit();

        page.WaitForAssertion(() => page.Find(Status).TextContent.Should().Be("Tercihlerin kaydedildi."));
        page.Find(Status).ClassList.Should().Contain("alert--success");
        using var sent = JsonDocument.Parse(Api.BodiesSentTo(HttpMethod.Put, SettingsPath).Single()!);
        sent.RootElement.GetProperty("isProfilePublic").GetBoolean().Should().BeTrue();
        sent.RootElement.GetProperty("locale").GetString().Should().Be("tr");
        sent.RootElement.GetProperty("timeZone").GetString().Should().Be("Europe/Berlin");
        sent.RootElement.GetProperty("avatarId").GetString().Should().Be("ninja-11");
        Api.BodiesSentTo(HttpMethod.Get, Me).Count().Should().Be(2, "the page loads once and the session refreshes once");
    }

    [Fact]
    public void Saving_only_the_avatar_keeps_a_public_profile_public()
    {
        Api.On(HttpMethod.Get, Me, Account(isProfilePublic: true));
        Api.On(HttpMethod.Put, SettingsPath, Account(isProfilePublic: true, avatarId: "ninja-05"));
        var page = Render<SettingsPage>();

        page.WaitForElement("input[aria-describedby=settings-visibility-hint]").HasAttribute("checked").Should().BeTrue();
        page.Find("input[value=ninja-05]").Change("ninja-05");
        page.Find($"{Preferences} form").Submit();

        page.WaitForAssertion(() => page.Find($"{Preferences} .alert--success").TextContent.Should().Contain("kaydedildi"));
        using var sent = JsonDocument.Parse(Api.BodiesSentTo(HttpMethod.Put, SettingsPath).Single()!);
        sent.RootElement.GetProperty("isProfilePublic").GetBoolean().Should().BeTrue("a new avatar must not hide a public profile");
        sent.RootElement.GetProperty("avatarId").GetString().Should().Be("ninja-05");
    }

    [Fact]
    public void Saving_the_profile_sends_trimmed_text_and_shows_what_was_stored()
    {
        Api.On(HttpMethod.Get, Me, Account());
        Api.On(HttpMethod.Put, ProfilePath, Account(displayName: "Yeni Ad", bio: "Merhaba"));
        var page = Render<SettingsPage>();

        page.WaitForElement("input[autocomplete=nickname]").Change("  Yeni Ad ");
        page.Find("#settings-bio").Input("  Merhaba  ");
        page.Find("#settings-bio-counter").TextContent.Should().Contain("11/300");
        page.Find("section[aria-labelledby=settings-profile] form").Submit();

        page.WaitForAssertion(() => page.Find("section[aria-labelledby=settings-profile] .alert--success").TextContent.Should().Be("Profilin kaydedildi."));
        using var sent = JsonDocument.Parse(Api.BodiesSentTo(HttpMethod.Put, ProfilePath).Single()!);
        sent.RootElement.GetProperty("displayName").GetString().Should().Be("Yeni Ad");
        sent.RootElement.GetProperty("bio").GetString().Should().Be("Merhaba");
        page.Find("input[autocomplete=nickname]").GetAttribute("value").Should().Be("Yeni Ad");
        page.Find("#settings-bio-counter").TextContent.Should().Contain("7/300", "the counter counts the stored text, not the spaces around it");
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
            PasswordPath,
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
        Api.BodiesSentTo(HttpMethod.Post, PasswordPath).Single().Should().Contain("\"currentPassword\":\"not my password\"");
    }

    /// <summary>
    /// The form keeps its model (cleared in place): a new one would rebuild the whole form, so the message would arrive
    /// in a brand-new live region, which screen readers often skip, and the focused button would be replaced.
    /// </summary>
    [Fact]
    public void Changing_the_password_shows_the_message_and_clears_the_fields()
    {
        Api.On(HttpMethod.Get, Me, Account());
        Api.On(HttpMethod.Post, PasswordPath, null, HttpStatusCode.NoContent);
        var page = Render<SettingsPage>();

        const string Section = "section[aria-labelledby=settings-password]";
        const string Passwords = $"{Section} input[type=password]";
        page.WaitForElements(Passwords)[0].Change("my old password");
        var feedback = page.FindComponent<PasswordSection>().FindComponent<SectionFeedback>().Instance;
        page.FindAll(Passwords)[1].Change("a long new passphrase");
        page.FindAll(Passwords)[2].Change("a long new passphrase");
        page.Find($"{Section} form").Submit();

        page.WaitForAssertion(() => page.Find($"{Section} p[role=status]").TextContent.Should().Be("Şifren değişti. Diğer cihazlardaki oturumların kapatıldı."));
        page.Find($"{Section} p[role=status]").ClassList.Should().Contain("alert--success");
        page.FindAll(Passwords).Should().HaveCount(3).And.OnlyContain(input => string.IsNullOrEmpty(input.GetAttribute("value")));
        page.FindComponent<PasswordSection>().FindComponent<SectionFeedback>().Instance.Should()
            .BeSameAs(feedback, "the status region that announces the change must be the one that was already on the page");
        using var sent = JsonDocument.Parse(Api.BodiesSentTo(HttpMethod.Post, PasswordPath).Single()!);
        sent.RootElement.GetProperty("currentPassword").GetString().Should().Be("my old password");
        sent.RootElement.GetProperty("newPassword").GetString().Should().Be("a long new passphrase");
    }

    [Fact]
    public void A_server_error_while_loading_offers_a_retry()
    {
        Api.OnProblem(HttpMethod.Get, Me, HttpStatusCode.ServiceUnavailable, "service_unavailable");
        var page = Render<SettingsPage>();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Biraz sonra tekrar dene"));
        Api.On(HttpMethod.Get, Me, Account());
        page.FindAll("button").Single(button => HasText(button, "Tekrar dene")).Click();

        page.WaitForElement("section[aria-labelledby=settings-profile]");
        page.FindAll(".alert--error").Should().BeEmpty();
    }

    [Fact]
    public void An_ended_session_while_loading_offers_no_retry_and_reloads_the_session()
    {
        var page = Render<SettingsPage>();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("giriş yap"));
        page.FindAll("button").Should().NotContain(button => HasText(button, "Tekrar dene"), "trying again cannot bring the session back");
        page.WaitForAssertion(() => Api.BodiesSentTo(HttpMethod.Get, Me).Should().HaveCount(2, "the page, then the session that learns it has ended"));
    }

    [Fact]
    public async Task An_ended_session_sends_the_user_to_sign_in_when_the_page_loads()
    {
        Api.On(HttpMethod.Get, Me, Account());
        await Services.GetRequiredService<AuthSession>().RefreshAsync();
        Api.OnProblem(HttpMethod.Get, Me, HttpStatusCode.Unauthorized, "unauthorized");
        Api.AnswerAsynchronously = true;

        var app = RenderApp("settings");

        var navigation = Services.GetRequiredService<NavigationManager>();
        app.WaitForAssertion(() => navigation.Uri.Should().Be(SignInRedirect));
    }

    [Fact]
    public void An_ended_session_sends_the_user_to_sign_in_when_a_card_saves()
    {
        Api.AnswerAsynchronously = true;
        Api.On(HttpMethod.Get, Me, Account());
        var app = RenderApp("settings");
        app.WaitForElement("section[aria-labelledby=settings-profile] form");

        Api.OnProblem(HttpMethod.Put, ProfilePath, HttpStatusCode.Unauthorized, "unauthorized");
        Api.OnProblem(HttpMethod.Get, Me, HttpStatusCode.Unauthorized, "unauthorized");
        app.Find("section[aria-labelledby=settings-profile] form").Submit();

        var navigation = Services.GetRequiredService<NavigationManager>();
        app.WaitForAssertion(() => navigation.Uri.Should().Be(SignInRedirect));
    }

    /// <summary>
    /// Through the real router: a save refreshes the session, and AuthorizeRouteView must keep the page (its success
    /// message and the other cards' unsaved edits) instead of showing "authorizing" and building it again.
    /// </summary>
    [Fact]
    public void Saving_in_the_running_app_keeps_the_page_its_message_and_unsaved_edits()
    {
        Api.AnswerAsynchronously = true;
        Api.On(HttpMethod.Get, Me, Account());
        Api.On(HttpMethod.Put, SettingsPath, Account(avatarId: "ninja-11"));
        var app = RenderApp("settings");

        app.WaitForElement("input[value=ninja-11]").Change("ninja-11");
        app.Find("input[autocomplete=nickname]").Change("Henüz kaydedilmedi");
        Api.On(HttpMethod.Get, Me, Account(avatarId: "ninja-11"));
        app.Find($"{Preferences} form").Submit();

        app.WaitForAssertion(() => app.Find($"{Preferences} .alert--success").TextContent.Should().Be("Tercihlerin kaydedildi."));
        Services.GetRequiredService<AuthSession>().Me!.AvatarId.Should().Be("ninja-11", "the session was refreshed");
        app.Find("input[autocomplete=nickname]").GetAttribute("value").Should().Be("Henüz kaydedilmedi");
        Api.BodiesSentTo(HttpMethod.Get, Me).Should().HaveCount(3, "the session, the page and one refresh; a rebuilt page would load again");
    }

    [Fact]
    public async Task Deleting_the_account_needs_the_checkbox_then_reloads_the_app_on_home()
    {
        Api.AnswerAsynchronously = true;
        Api.On(HttpMethod.Get, Me, Account());
        Api.On(HttpMethod.Delete, Me, null, HttpStatusCode.NoContent);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        var app = RenderApp("settings");

        const string Section = "section[aria-labelledby=settings-delete]";
        app.WaitForElement($"{Section} button[type=submit]").HasAttribute("disabled").Should().BeTrue("the user has not confirmed yet");

        // Awaited: while an answer's continuation still holds the renderer (asynchronous answers), the synchronous
        // Change only queues the event, so the button could be read before the checkbox reached the form.
        await app.Find($"{Section} input[type=password]").ChangeAsync(new ChangeEventArgs { Value = "my password!!" });
        await app.Find($"{Section} input[type=checkbox]").ChangeAsync(new ChangeEventArgs { Value = true });
        app.Find($"{Section} button[type=submit]").HasAttribute("disabled").Should().BeFalse();
        await app.Find($"{Section} form").SubmitAsync();

        Api.BodiesSentTo(HttpMethod.Delete, Me).Single().Should().Contain("\"password\":\"my password!!\"");
        var reload = navigation.History.First();
        reload.Uri.Should().BeEmpty("the app starts again on the home page");
        reload.Options.ForceLoad.Should().BeTrue("a fresh app finds no session instead of showing the deleted account in the meantime");
        reload.Options.ReplaceHistoryEntry.Should().BeTrue("Back must not return to the settings of a deleted account");
        Api.Requests.Should().NotContain(
            request => request.Method == HttpMethod.Post && string.Equals(request.PathAndQuery, LogoutPath, StringComparison.Ordinal),
            "DELETE /me already ended the session; signing out in place would only delay the reload");
    }

    /// <summary>GET /me as the API sends it: camelCase, every field present.</summary>
    private static object Account(
        bool isProfilePublic = true,
        string locale = "tr",
        string timeZone = "Europe/Istanbul",
        string avatarId = "ninja-03",
        string displayName = "Gece Okuru",
        string? bio = "Her gece bir bölüm.") => new
        {
            id = Guid.Parse("6f1c2a9e-1d4b-4c7a-9a51-2f0e8d3b7c11"),
            username = "gece_okuru",
            email = "gece@test.kunai",
            displayName,
            bio,
            avatarId,
            isProfilePublic,
            locale,
            timeZone,
            createdAt = "2026-01-15T09:30:00Z",
        };

    private static bool HasText(AngleSharp.Dom.IElement element, string text) =>
        string.Equals(element.TextContent.Trim(), text, StringComparison.Ordinal);

    /// <summary>Renders the whole app (router, sign-in check, layout) at <paramref name="path"/>, like a browser tab.</summary>
    private IRenderedComponent<Routes> RenderApp(string path)
    {
        // FocusOnNavigate moves the focus to the page's heading through JS.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        return Render<Routes>();
    }
}
