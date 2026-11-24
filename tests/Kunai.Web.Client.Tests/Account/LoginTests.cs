using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Account;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Account;

public sealed class LoginTests : KunaiTestContext
{
    [Fact]
    public void Wrong_credentials_show_the_turkish_message_for_the_api_code()
    {
        Api.OnProblem(HttpMethod.Post, "/api/v1/auth/login", HttpStatusCode.Unauthorized, "invalid_credentials");
        var page = Render<Login>();

        page.Find("input[autocomplete=username]").Change("ninja");
        page.Find("input[type=password]").Change("wrong password!!");
        page.Find("form").Submit();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("şifre yanlış"));
    }

    [Fact]
    public void Successful_sign_in_uses_the_cookie_and_goes_home()
    {
        Api.On(HttpMethod.Post, "/api/v1/auth/login", null, HttpStatusCode.NoContent);
        var page = Render<Login>();

        page.Find("input[autocomplete=username]").Change("ninja");
        page.Find("input[type=password]").Change("correct password");
        SignInAs("ninja");
        page.Find("form").Submit();

        page.WaitForAssertion(() => Services.GetRequiredService<NavigationManager>().Uri.Should().Be("http://localhost/"));
        Api.BodiesSentTo(HttpMethod.Post, "/api/v1/auth/login").Single().Should().Contain("\"useCookie\":true");
    }
}
