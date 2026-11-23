using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Kunai.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
                values: new object[,]
                {
                    { new Guid("0193e2a0-0000-7000-8000-000000000001"), "8b0e7f4e-admin", "admin", "ADMIN" },
                    { new Guid("0193e2a0-0000-7000-8000-000000000002"), "8b0e7f4e-moderator", "moderator", "MODERATOR" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_items_created_by_user_id",
                table: "items",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_title",
                table: "items",
                column: "title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_items_created_by_user_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "ix_items_title",
                table: "items");

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0193e2a0-0000-7000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0193e2a0-0000-7000-8000-000000000002"));

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
