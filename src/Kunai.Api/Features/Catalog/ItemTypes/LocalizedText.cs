namespace Kunai.Api.Features.Catalog.ItemTypes;

/// <summary>A label in both languages, so one cacheable response serves every client.</summary>
public sealed record LocalizedText(string Tr, string En);
