using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kunai.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    cover_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    creators = table.Column<string[]>(type: "text[]", nullable: false),
                    genres = table.Column<string[]>(type: "text[]", nullable: false),
                    progress_total = table.Column<int>(type: "integer", nullable: true),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    catalog_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    anime_format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    isbn13 = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    platforms = table.Column<string[]>(type: "text[]", nullable: true),
                    manga_format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    volume_count = table.Column<int>(type: "integer", nullable: true),
                    runtime_minutes = table.Column<int>(type: "integer", nullable: true),
                    season_count = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_items", x => x.id);
                    table.CheckConstraint("ck_items_progress_total", "progress_total IS NULL OR progress_total > 0");
                    table.CheckConstraint("ck_items_year", "year IS NULL OR year BETWEEN 1000 AND 3000");
                });

            migrationBuilder.CreateIndex(
                name: "ix_items_isbn13",
                table: "items",
                column: "isbn13");

            migrationBuilder.CreateIndex(
                name: "ix_items_source_external_id",
                table: "items",
                columns: new[] { "source", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_items_type_catalog_status",
                table: "items",
                columns: new[] { "type", "catalog_status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "items");
        }
    }
}
