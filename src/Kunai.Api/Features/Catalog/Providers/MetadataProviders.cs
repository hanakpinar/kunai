namespace Kunai.Api.Features.Catalog.Providers;

/// <summary>Finds the provider for a source, and the providers to search for a type (primary first).</summary>
public sealed class MetadataProviders(IEnumerable<IMetadataProvider> providers)
{
    private readonly IReadOnlyList<IMetadataProvider> _providers = providers.ToList();

    public IMetadataProvider? For(ItemSource source) => _providers.FirstOrDefault(p => p.Source == source);

    public IReadOnlyList<IMetadataProvider> ForSearch(ItemType type) =>
        ItemTypes.ItemTypeRegistry.For(type).Sources
            .Select(For)
            .OfType<IMetadataProvider>()
            .Where(p => p.Supports(type))
            .ToList();
}
