using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LegacyImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "importedAt",
                table: "routes",
                type: "timestamp(3) with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "importedAt",
                table: "payments",
                type: "timestamp(3) with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "importedAt",
                table: "invoices",
                type: "timestamp(3) with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "importedAt",
                table: "delivery_notes",
                type: "timestamp(3) with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "importedAt",
                table: "courier_statements",
                type: "timestamp(3) with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "importedAt",
                table: "cash_remittances",
                type: "timestamp(3) with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "legacy_mappings",
                columns: table => new
                {
                    entity = table.Column<string>(type: "text", nullable: false),
                    legacyId = table.Column<string>(type: "text", nullable: false),
                    newId = table.Column<string>(type: "text", nullable: false),
                    hash = table.Column<string>(type: "text", nullable: false),
                    importedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_mappings", x => new { x.entity, x.legacyId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_mappings_entity_newId",
                table: "legacy_mappings",
                columns: new[] { "entity", "newId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legacy_mappings");

            migrationBuilder.DropColumn(
                name: "importedAt",
                table: "routes");

            migrationBuilder.DropColumn(
                name: "importedAt",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "importedAt",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "importedAt",
                table: "delivery_notes");

            migrationBuilder.DropColumn(
                name: "importedAt",
                table: "courier_statements");

            migrationBuilder.DropColumn(
                name: "importedAt",
                table: "cash_remittances");
        }
    }
}
