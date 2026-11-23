using Kunai.Api.Features.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Api.IntegrationTests.Infrastructure;

public static class TestRoles
{
    /// <summary>A signed-in client whose token carries the moderator role.</summary>
    public static async Task<HttpClient> CreateModeratorClientAsync(this KunaiApiFactory factory)
    {
        var user = await factory.RegisterConfirmedAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var account = await users.FindByNameAsync(user.Username);
            await users.AddToRoleAsync(account!, Roles.Moderator);
        }

        return await factory.CreateSignedInClientAsync(user);
    }
}
