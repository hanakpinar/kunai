using Kunai.Api.Features.Identity;

namespace Kunai.Api.UnitTests.Identity;

public sealed class UsernameRulesTests
{
    [Theory]
    [InlineData("kunai_fan")]
    [InlineData("abc")]
    [InlineData("a23456789012345678901234")]
    public void Valid_usernames_pass(string username) =>
        UsernameRules.Validate(username).Should().BeNull();

    [Theory]
    [InlineData("ab")]
    [InlineData("1abc")]
    [InlineData("_abc")]
    [InlineData("a234567890123456789012345")]
    [InlineData("ılgaz")]
    [InlineData("has space")]
    [InlineData("UPPER")]
    public void Invalid_usernames_fail(string username) =>
        UsernameRules.Validate(username)!.Fields!["username"].Should().Equal(UsernameRules.InvalidCode);

    [Fact]
    public void Reserved_usernames_fail() =>
        UsernameRules.Validate("admin")!.Fields!["username"].Should().Equal(UsernameRules.ReservedCode);

    [Fact]
    public void Normalize_uses_invariant_lowercasing_even_on_a_turkish_thread()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            UsernameRules.Normalize("  KIRAN_IKI ").Should().Be("kiran_iki");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
