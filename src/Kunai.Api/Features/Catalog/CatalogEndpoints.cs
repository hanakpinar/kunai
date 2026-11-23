using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Features.Catalog.ItemTypes;
using Kunai.Api.Features.Catalog.Search;

namespace Kunai.Api.Features.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder api)
    {
        GetItemTypes.Map(api);
        SearchCatalog.Map(api);
        ImportItem.Map(api);
        GetItem.Map(api);
        AddManualBook.Map(api);
        return api;
    }
}
