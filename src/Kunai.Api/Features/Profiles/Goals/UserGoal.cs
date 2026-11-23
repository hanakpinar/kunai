using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kunai.Api.Features.Profiles.Goals;

/// <summary>A yearly target ("mission" in the app): e.g. 30 books in 2026.</summary>
public sealed class UserGoal
{
    public Guid UserId { get; init; }

    public int Year { get; init; }

    public ItemType Type { get; init; }

    public int Target { get; set; }
}

internal sealed class UserGoalConfiguration : IEntityTypeConfiguration<UserGoal>
{
    public void Configure(EntityTypeBuilder<UserGoal> goal)
    {
        goal.ToTable("user_goals", table => table.HasCheckConstraint("ck_user_goals_target", "target BETWEEN 1 AND 10000"));
        goal.HasKey(g => new { g.UserId, g.Year, g.Type });
        goal.Property(g => g.Type).HasConversion<string>().HasMaxLength(16);
        goal.HasOne<AppUser>().WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
