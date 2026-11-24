using Kunai.Web.Client.Api.Models;

namespace Kunai.Web.Client.Pages.Profile;

/// <summary>One target box per item type; an empty box means "no mission for this type".</summary>
public sealed class GoalsForm
{
    public const int MinTarget = 1;
    public const int MaxTarget = 10_000;

    public IReadOnlyList<GoalRow> Rows { get; private init; } = [];

    public static GoalsForm From(IEnumerable<GoalProgress>? goals)
    {
        var existing = goals?.ToList() ?? [];
        return new GoalsForm
        {
            Rows = [.. Enum.GetValues<ItemType>().Select(type => new GoalRow { Type = type, Target = existing.FirstOrDefault(g => g.Type == type)?.Target })],
        };
    }

    public bool IsValid => Rows.All(r => r.Target is null or (>= MinTarget and <= MaxTarget));

    /// <summary>The PUT body: every filled-in type, none of the empty ones.</summary>
    public GoalsRequest ToRequest() => new()
    {
        Targets = [.. Rows.Where(r => r.Target is not null).Select(r => new GoalTarget { Type = r.Type, Target = r.Target })],
    };
}

public sealed class GoalRow
{
    public ItemType Type { get; init; }

    public int? Target { get; set; }
}
