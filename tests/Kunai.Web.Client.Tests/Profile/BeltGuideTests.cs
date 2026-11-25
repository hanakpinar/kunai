using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Profile;

using Microsoft.AspNetCore.Components;

namespace Kunai.Web.Client.Tests.Profile;

public sealed class BeltGuideTests : ProfileTestContext
{
    [Fact]
    public void Only_css_hex_colors_paint_the_knot()
    {
        Api.On(HttpMethod.Get, "/api/v1/ranks", new object[]
        {
            Belt("white", 0, "#fff"),
            Belt("yellow", 50, "#f5c5"),
            Belt("orange", 100, "#f08a24"),
            Belt("green", 200, "#2e9e4f80"),
            Belt("blue", 400, "#12345"),
            Belt("brown", 800, "#1234567"),
            Belt("black", 1600, "black"),
        });

        var page = Render<BeltGuide>();

        page.WaitForAssertion(() => page.FindAll(".belts__item").Should().HaveCount(7));
        page.FindAll(".belt").Select(b => b.GetAttribute("style")).Should().Equal(
            "--belt-color: #fff",
            "--belt-color: #f5c5",
            "--belt-color: #f08a24",
            "--belt-color: #2e9e4f80",
            null,
            null,
            null);
        page.Find(".points").TextContent.Should().Contain("Kitap: 3 puan").And.Contain("Oyun: 4 puan");
    }

    [Fact]
    public void A_failed_load_offers_a_retry()
    {
        Api.OnProblem(HttpMethod.Get, "/api/v1/ranks", HttpStatusCode.TooManyRequests, "rate_limited");
        var page = Render<BeltGuide>();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Biraz bekleyip tekrar dene"));
        var heading = page.Find("#belt-guide-title");
        heading.GetAttribute("tabindex").Should().Be("-1");
        var headingReference = heading.GetAttribute("blazor:elementreference");
        Api.On(HttpMethod.Get, "/api/v1/ranks", new object[] { Belt("white", 0, "#f4f4f0") });
        ButtonNamed(page, "Tekrar dene").Click();

        page.WaitForAssertion(() => page.FindAll(".belts__item").Should().ContainSingle());
        page.FindAll(".alert--error").Should().BeEmpty();
        // "Tekrar dene" had the focus and is gone: the section's heading takes it, not the top of the page.
        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].Should().BeOfType<ElementReference>().Which.Id.Should().Be(headingReference);
    }

    private static object Belt(string code, int minPoints, string color) =>
        new { code, label = new { tr = $"{code} kuşak", en = $"{code} belt" }, minPoints, color };
}
