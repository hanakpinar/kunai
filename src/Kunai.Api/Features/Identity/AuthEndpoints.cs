namespace Kunai.Api.Features.Identity;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        Register.Map(auth);
        ConfirmEmail.Map(auth);
        ResendConfirmation.Map(auth);
        UsernameAvailability.Map(auth);

        return api;
    }
}
