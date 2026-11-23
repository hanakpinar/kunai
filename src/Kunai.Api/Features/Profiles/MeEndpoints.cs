namespace Kunai.Api.Features.Profiles;

public static class MeEndpoints
{
    /// <summary>Everything under /me is the caller's own, private view; it always requires sign-in.</summary>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder api)
    {
        var me = api.MapGroup("/me").WithTags("Me");

        GetMe.Map(me);

        return api;
    }
}
