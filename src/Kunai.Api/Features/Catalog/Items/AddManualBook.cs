using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Identity;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Kunai.Api.Features.Catalog.Items;

/// <summary>
/// Books that no provider knows (many Turkish editions) can be added by hand. They wait for a moderator:
/// until approved, only the person who added them can see them and put them on a shelf.
/// </summary>
public static class AddManualBook
{
    public const int DailyLimit = 10;
    public const string LimitReached = "manual_item_limit";
    public const string BookExists = "book_exists";
    public const string BookPendingReview = "book_pending_review";

    public static void Map(IEndpointRouteBuilder api) =>
        api.MapPost("/items", HandleAsync)
            .WithTags("Catalog")
            .WithName("AddManualBook")
            .WithSummary("Add a book that search cannot find; it is reviewed before others can see it")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    internal static async Task<Results<Created<ItemResponse>, ProblemHttpResult>> HandleAsync(
        ManualBookRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var since = time.GetUtcNow().AddDays(-1);
        var addedToday = await db.Set<Item>().CountAsync(i => i.CreatedByUserId == userId && i.CreatedAt >= since, cancellationToken);
        if (addedToday >= DailyLimit)
        {
            return Error.BusinessRule(LimitReached, $"You can add at most {DailyLimit} books a day.").ToProblem();
        }

        if (request.Isbn13 is { } isbn
            && await db.Set<Book>().AsNoTracking().FirstOrDefaultAsync(b => b.Isbn13 == isbn && b.CatalogStatus != CatalogStatus.Rejected, cancellationToken) is { } existing)
        {
            // Point to the existing book only if the caller may see it; someone else's submission stays private.
            return GetItem.CanSee(existing, principal)
                ? Error.Conflict(BookExists, $"A book with this ISBN is already in the catalog: {existing.Id}.").ToProblem()
                : Error.Conflict(BookPendingReview, "Someone already submitted this book; it is waiting for review.").ToProblem();
        }

        var itemId = Guid.CreateVersion7();
        var book = new Book
        {
            Id = itemId,
            Title = request.Title!.Trim(),
            ExternalId = $"manual:{itemId}",
            Source = ItemSource.Manual,
            CatalogStatus = CatalogStatus.PendingReview,
            CreatedByUserId = userId,
            Year = request.Year,
            Description = request.Description?.Trim(),
            Creators = (request.Authors ?? []).Select(a => a.Trim()).Where(a => a.Length > 0).Distinct(StringComparer.Ordinal).ToList(),
            ProgressTotal = request.PageCount,
            Isbn13 = request.Isbn13,
            Publisher = request.Publisher?.Trim(),
        };
        db.Add(book);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/v1/items/{book.Id}", ItemResponse.From(book));
    }
}

public sealed class ManualBookRequest : IValidatableObject
{
    [Required(ErrorMessage = "required")]
    [StringLength(Item.TitleMaxLength, MinimumLength = 1, ErrorMessage = "title_length")]
    public string? Title { get; init; }

    [MaxLength(10, ErrorMessage = "too_many")]
    public IReadOnlyList<string>? Authors { get; init; }

    [Range(1000, 3000, ErrorMessage = "year_out_of_range")]
    public int? Year { get; init; }

    /// <summary>13 digits, no dashes; checked with its check digit.</summary>
    [RegularExpression("^97[89][0-9]{10}$", ErrorMessage = "isbn_invalid", MatchTimeoutInMilliseconds = 100)]
    public string? Isbn13 { get; init; }

    [Range(1, 100_000, ErrorMessage = "page_count_out_of_range")]
    public int? PageCount { get; init; }

    [StringLength(200, ErrorMessage = "too_long")]
    public string? Publisher { get; init; }

    [StringLength(Item.DescriptionMaxLength, ErrorMessage = "too_long")]
    public string? Description { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Isbn13 is not null && !HasValidCheckDigit(Isbn13))
        {
            yield return new ValidationResult("isbn_invalid", [nameof(Isbn13)]);
        }

        if (Authors is not null && Authors.Any(a => a is null))
        {
            yield return new ValidationResult("required", [nameof(Authors)]);
        }
        else if (Authors is not null && Authors.Any(a => a.Length > 200))
        {
            yield return new ValidationResult("too_long", [nameof(Authors)]);
        }
    }

    private static bool HasValidCheckDigit(string isbn)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (isbn[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return (10 - (sum % 10)) % 10 == isbn[12] - '0';
    }
}
