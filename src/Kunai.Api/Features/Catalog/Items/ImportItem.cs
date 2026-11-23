using System.ComponentModel.DataAnnotations;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Catalog.Providers;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Kunai.Api.Features.Catalog.Items;

public static class ImportItem
{
    public static void Map(IEndpointRouteBuilder api) =>
        api.MapPost("/items/import", HandleAsync)
            .WithTags("Catalog")
            .WithName("ImportItem")
            .WithSummary("Add a search result to the catalog")
            .WithDescription("Safe to repeat: 201 when the item is new, 200 with the existing item otherwise. Call it when a search hit has no id, then add the id to your shelf.")
            .RequireRateLimiting(RateLimitPolicies.Import)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    internal static async Task<Results<Created<ItemResponse>, Ok<ItemResponse>, ProblemHttpResult>> HandleAsync(
        ImportItemRequest request,
        AppDbContext db,
        MetadataProviders providers,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ExternalRef.TryParse(request.Ref, out var parsed))
        {
            return Error.ValidationField("ref", "ref_invalid", "Use a ref from a search result, e.g. \"tmdb:movie:603\".").ToProblem();
        }

        var reference = parsed.Value;
        if (await FindAsync(db, reference, cancellationToken) is { } existing)
        {
            return TypedResults.Ok(ItemResponse.From(existing));
        }

        if (providers.For(reference.Source) is not { } provider)
        {
            return ProviderErrors.NotConfigured(reference.Source).ToProblem();
        }

        var metadata = await provider.GetAsync(reference, cancellationToken);
        if (!metadata.IsSuccess)
        {
            if (metadata.Error.Type == ErrorType.Unavailable)
            {
                httpContext.Response.Headers.RetryAfter = "30";
            }

            return metadata.Error.ToProblem();
        }

        var item = ItemFactory.Create(metadata.Value with { Ref = reference });
        db.Add(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Someone imported the same item a moment ago; return theirs.
            db.ChangeTracker.Clear();
            var winner = await FindAsync(db, reference, cancellationToken);
            return TypedResults.Ok(ItemResponse.From(winner!));
        }

        return TypedResults.Created($"/api/v1/items/{item.Id}", ItemResponse.From(item));
    }

    private static Task<Item?> FindAsync(AppDbContext db, ExternalRef reference, CancellationToken cancellationToken) =>
        db.Set<Item>().AsNoTracking()
            .SingleOrDefaultAsync(i => i.Source == reference.Source && i.ExternalId == reference.StoredId, cancellationToken);
}

public sealed class ImportItemRequest
{
    /// <summary>The "ref" of a search result, e.g. "tmdb:movie:603".</summary>
    [Required(ErrorMessage = "required")]
    [StringLength(150, ErrorMessage = "too_long")]
    public string? Ref { get; init; }
}
