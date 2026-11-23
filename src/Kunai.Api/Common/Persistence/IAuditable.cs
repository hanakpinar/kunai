namespace Kunai.Api.Common.Persistence;

/// <summary>Entities whose CreatedAt/UpdatedAt are stamped automatically on save.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset UpdatedAt { get; set; }
}
