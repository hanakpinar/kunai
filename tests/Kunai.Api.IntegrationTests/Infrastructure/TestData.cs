namespace Kunai.Api.IntegrationTests.Infrastructure;

public static class TestData
{
    public const string Password = "correct horse battery staple";

    /// <summary>A unique, valid username such as "u1a2b3c4d5e6f".</summary>
    public static string NewUsername() => "u" + Guid.NewGuid().ToString("N")[..12];

    public static string EmailFor(string username) => $"{username}@test.kunai";
}
