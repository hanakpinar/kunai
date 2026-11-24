using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Components;

namespace Kunai.Web.Tests;

/// <summary>
/// Every page must load on a reload or a deep link. The host only serves the shell; the browser decides who may
/// see a page. A page's [Authorize] once made this host answer 500, which component tests cannot see.
/// </summary>
public sealed partial class PageRoutesTests(WebHostFactory factory) : IClassFixture<WebHostFactory>
{
    public static TheoryData<string> Routes() => [.. RoutePaths()];

    [Fact]
    public void Every_page_is_covered() =>
        RoutePaths().Should().Contain(["/", "/shelf", "/settings", "/items/new", "/search", "/u/ninja"]);

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_direct_visit_serves_the_app_shell(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("blazor.web").And.Contain("Kunai yükleniyor");
    }

    /// <summary>The route of every page in the client assembly, with sample values for its parameters.</summary>
    private static IEnumerable<string> RoutePaths() =>
        typeof(Kunai.Web.Client._Imports).Assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>())
            .Select(route => Sample(route.Template))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    /// <summary>Fills route parameters with sample values: a GUID for :guid, a username otherwise.</summary>
    private static string Sample(string template) =>
        Parameter().Replace(template, match => match.Groups["constraint"].Value.Equals("guid", StringComparison.OrdinalIgnoreCase)
            ? "0192f8a4-7c1e-7d4a-9b8e-3f2a1c0d5e6f"
            : "ninja");

    [GeneratedRegex(@"\{(?<name>[^}:?]+)(:(?<constraint>[^}?]+))?\??\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Parameter();
}
