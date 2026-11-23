using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kunai.Api.Features.Catalog.Items;

/// <summary>Table-per-hierarchy: one "items" table, a "type" discriminator and nullable type-specific columns.</summary>
internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> item)
    {
        item.ToTable("items", table =>
        {
            table.HasCheckConstraint("ck_items_year", "year IS NULL OR year BETWEEN 1000 AND 3000");
            table.HasCheckConstraint("ck_items_progress_total", "progress_total IS NULL OR progress_total > 0");
        });
        item.Property(i => i.Id).ValueGeneratedNever();
        item.Property(i => i.Type).HasConversion<string>().HasMaxLength(16);
        item.HasDiscriminator(i => i.Type)
            .HasValue<Book>(ItemType.Book)
            .HasValue<Movie>(ItemType.Movie)
            .HasValue<Series>(ItemType.Series)
            .HasValue<Game>(ItemType.Game)
            .HasValue<Anime>(ItemType.Anime)
            .HasValue<Manga>(ItemType.Manga);

        item.Property(i => i.Title).HasMaxLength(Item.TitleMaxLength);
        item.Property(i => i.OriginalTitle).HasMaxLength(Item.TitleMaxLength);
        item.Property(i => i.Description).HasMaxLength(Item.DescriptionMaxLength);
        item.Property(i => i.CoverUrl).HasMaxLength(1000);
        item.Property(i => i.Source).HasConversion<string>().HasMaxLength(16);
        item.Property(i => i.ExternalId).HasMaxLength(100);
        item.Property(i => i.CatalogStatus).HasConversion<string>().HasMaxLength(16);

        // Two users importing the same movie at once end up with one row.
        item.HasIndex(i => new { i.Source, i.ExternalId }).IsUnique();
        item.HasIndex(i => new { i.Type, i.CatalogStatus });
    }
}

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> book)
    {
        book.Property(b => b.Isbn13).HasMaxLength(13);
        book.Property(b => b.Publisher).HasMaxLength(200);
        book.HasIndex(b => b.Isbn13);
    }
}

internal sealed class AnimeConfiguration : IEntityTypeConfiguration<Anime>
{
    public void Configure(EntityTypeBuilder<Anime> anime) =>
        anime.Property(a => a.Format).HasColumnName("anime_format").HasMaxLength(32);
}

internal sealed class MangaConfiguration : IEntityTypeConfiguration<Manga>
{
    public void Configure(EntityTypeBuilder<Manga> manga) =>
        manga.Property(m => m.Format).HasColumnName("manga_format").HasMaxLength(32);
}
