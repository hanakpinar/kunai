namespace Kunai.Api.Features.Library;

public static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder api)
    {
        var shelf = api.MapGroup("/me/shelf").WithTags("Shelf");

        GetMyShelf.Map(shelf);
        GetShelfEntry.Map(shelf);
        UpsertShelfEntry.Map(shelf);
        DeleteShelfEntry.Map(shelf);

        return api;
    }
}
