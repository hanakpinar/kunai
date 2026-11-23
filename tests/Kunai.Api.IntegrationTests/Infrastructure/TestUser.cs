namespace Kunai.Api.IntegrationTests.Infrastructure;

public sealed record TestUser(string Username, string Email, string Password);

public sealed record Tokens(string AccessToken, string RefreshToken);
