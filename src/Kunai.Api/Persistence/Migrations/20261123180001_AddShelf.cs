using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kunai.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShelf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shelf_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    started_on = table.Column<DateOnly>(type: "date", nullable: true),
                    finished_on = table.Column<DateOnly>(type: "date", nullable: true),
                    progress = table.Column<int>(type: "integer", nullable: true),
                    review = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shelf_entries", x => x.id);
                    table.CheckConstraint("ck_shelf_entries_dates", "started_on IS NULL OR finished_on IS NULL OR finished_on >= started_on");
                    table.CheckConstraint("ck_shelf_entries_progress", "progress IS NULL OR progress >= 0");
                    table.CheckConstraint("ck_shelf_entries_rating", "rating IS NULL OR rating BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_shelf_entries_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shelf_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_shelf_entries_item_id",
                table: "shelf_entries",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_shelf_entries_user_id_finished_on",
                table: "shelf_entries",
                columns: new[] { "user_id", "finished_on" },
                filter: "finished_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_shelf_entries_user_id_item_id",
                table: "shelf_entries",
                columns: new[] { "user_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shelf_entries_user_id_status",
                table: "shelf_entries",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_shelf_entries_user_id_updated_at",
                table: "shelf_entries",
                columns: new[] { "user_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shelf_entries");
        }
    }
}
