using Kunai.Api.Features.Catalog.ItemTypes;

namespace Kunai.Api.Features.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder api)
    {
        GetItemTypes.Map(api);
        return api;
    }
}
