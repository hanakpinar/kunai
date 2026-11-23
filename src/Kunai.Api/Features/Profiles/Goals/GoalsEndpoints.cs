using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Kunai.Api.Common.Caching;
using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Identity;
using Kunai.Api.Features.Library;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Kunai.Api.Features.Profiles.Goals;

public static class GoalsEndpoints
{
    public static void MapMine(RouteGroupBuilder me) =>
        me.MapPut("/goals/{year:int:range(2000,2100)}", PutAsync)
            .WithName("PutMyGoals")
            .WithSummary("Set this year's targets (missions); replaces every target of that year")
            .ProducesValidationProblem();

    public static void MapUsers(RouteGroupBuilder users) =>
        users.MapGet("/{username}/goals/{year:int:range(2000,2100)}", GetAsync)
            .WithName("GetUserGoals")
            .WithSummary("A user's targets for a year with progress")
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound);

    internal static async Task<Results<Ok<GoalsResponse>, ProblemHttpResult>> PutAsync(
        int year,
        GoalsRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        HybridCache cache,
        CancellationToken cancellationToken)
    {
        var targets = request.Targets ?? [];
        if (targets.GroupBy(t => t.Type).Any(g => g.Count() > 1))
        {
            return Error.ValidationField("targets", "duplicate_type", "Each type may appear once.").ToProblem();
        }

        var userId = principal.GetUserId();
        await db.Set<UserGoal>().Where(g => g.UserId == userId && g.Year == year).ExecuteDeleteAsync(cancellationToken);
        db.AddRange(targets.Select(t => new UserGoal { UserId = userId, Year = year, Type = t.Type!.Value, Target = t.Target }));
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(CacheTags.User(userId), cancellationToken);

        return TypedResults.Ok(await ComputeAsync(db, userId, year, cancellationToken));
    }

    internal static async Task<Results<Ok<GoalsResponse>, ProblemHttpResult>> GetAsync(
        string username,
        int year,
        ClaimsPrincipal viewer,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await ProfileLookup.FindVisibleAsync(db, username, viewer, cancellationToken);
        return user is null
            ? ProfileLookup.NotFound.ToProblem()
            : TypedResults.Ok(await ComputeAsync(db, user.Id, year, cancellationToken));
    }

    private static async Task<GoalsResponse> ComputeAsync(AppDbContext db, Guid userId, int year, CancellationToken cancellationToken)
    {
        var goals = await db.Set<UserGoal>().AsNoTracking().Where(g => g.UserId == userId && g.Year == year).ToListAsync(cancellationToken);
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var done = await db.Set<ShelfEntry>().AsNoTracking()
            .Where(e => e.UserId == userId && e.Status == ShelfStatus.Completed && e.FinishedOn >= from && e.FinishedOn <= to)
            .GroupBy(e => e.Item.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Type, g => g.Count, cancellationToken);

        return new GoalsResponse(
            year,
            goals.OrderBy(g => g.Type)
                .Select(g =>
                {
                    var completed = done.GetValueOrDefault(g.Type);
                    return new GoalProgress(g.Type, g.Target, completed, Math.Min(100, completed * 100 / g.Target));
                })
                .ToList());
    }
}

public sealed class GoalsRequest
{
    [Required(ErrorMessage = "required")]
    [MaxLength(6, ErrorMessage = "too_many")]
    public IReadOnlyList<GoalTarget>? Targets { get; init; }
}

public sealed class GoalTarget
{
    [Required(ErrorMessage = "required")]
    public ItemType? Type { get; init; }

    [Range(1, 10_000, ErrorMessage = "target_out_of_range")]
    public int Target { get; init; }
}

public sealed record GoalsResponse(int Year, IReadOnlyList<GoalProgress> Goals);

/// <param name="Type">Item type.</param>
/// <param name="Target">How many to complete this year.</param>
/// <param name="Completed">Completed so far (by finish date).</param>
/// <param name="Percent">0-100.</param>
public sealed record GoalProgress(ItemType Type, int Target, int Completed, int Percent);
