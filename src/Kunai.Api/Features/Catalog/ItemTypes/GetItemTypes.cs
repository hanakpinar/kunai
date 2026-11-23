using Microsoft.AspNetCore.Http.HttpResults;

namespace Kunai.Api.Features.Catalog.ItemTypes;

public static class GetItemTypes
{
    public static void Map(IEndpointRouteBuilder api) =>
        api.MapGet("/item-types", Handle)
            .WithTags("Catalog")
            .WithName("GetItemTypes")
            .WithSummary("Item types, their allowed statuses with TR/EN labels, progress units and provider attribution")
            .AllowAnonymous()
            .CacheOutputHeaders();

    internal static Ok<ItemTypesResponse> Handle() =>
        TypedResults.Ok(new ItemTypesResponse(ItemTypeRegistry.All, ItemTypeRegistry.Sources));

    /// <summary>The registry only changes with a deployment; clients may cache it for an hour.</summary>
    private static RouteHandlerBuilder CacheOutputHeaders(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "public, max-age=3600";
            return await next(context);
        });
}

public sealed record ItemTypesResponse(IReadOnlyList<ItemTypeDefinition> Types, IReadOnlyList<SourceAttribution> Sources);
