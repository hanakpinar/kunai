using Kunai.Api.Features.Profiles.Avatars;
using Kunai.Api.Features.Profiles.Goals;
using Kunai.Api.Features.Profiles.Ranks;
using Kunai.Api.Features.Profiles.Stats;

namespace Kunai.Api.Features.Profiles;

public static class UsersEndpoints
{
    /// <summary>The public view of users; the same answer for every viewer, private profiles are 404.</summary>
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder api)
    {
        var users = api.MapGroup("/users").WithTags("Profiles");

        GetPublicProfile.Map(users);
        GetUserShelf.Map(users);
        GetUserStats.Map(users);
        GoalsEndpoints.MapUsers(users);

        GetRanks.Map(api);
        GetAvatar.Map(api);

        return api;
    }
}
