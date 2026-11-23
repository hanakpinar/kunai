using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Features.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kunai.Api.Features.Library;

internal sealed class ShelfEntryConfiguration : IEntityTypeConfiguration<ShelfEntry>
{
    public void Configure(EntityTypeBuilder<ShelfEntry> entry)
    {
        entry.ToTable("shelf_entries", table =>
        {
            table.HasCheckConstraint("ck_shelf_entries_rating", "rating IS NULL OR rating BETWEEN 1 AND 10");
            table.HasCheckConstraint("ck_shelf_entries_progress", "progress IS NULL OR progress >= 0");
            table.HasCheckConstraint("ck_shelf_entries_dates", "started_on IS NULL OR finished_on IS NULL OR finished_on >= started_on");
        });

        entry.Property(e => e.Id).ValueGeneratedNever();
        entry.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
        entry.Property(e => e.Review).HasMaxLength(ShelfEntry.ReviewMaxLength);
        entry.Property(e => e.Version).IsRowVersion();

        entry.HasOne<AppUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        entry.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);

        entry.HasIndex(e => new { e.UserId, e.ItemId }).IsUnique();
        entry.HasIndex(e => new { e.UserId, e.UpdatedAt });
        entry.HasIndex(e => new { e.UserId, e.Status });
        entry.HasIndex(e => new { e.UserId, e.FinishedOn }).HasFilter("finished_on IS NOT NULL");
    }
}
