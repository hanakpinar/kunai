namespace Kunai.Api.Features.Catalog;

/// <summary>One status vocabulary for every type; each type allows a subset and labels it its own way.</summary>
public enum ShelfStatus
{
    Planned,
    InProgress,
    Completed,
    Dropped,
    Paused,
}
