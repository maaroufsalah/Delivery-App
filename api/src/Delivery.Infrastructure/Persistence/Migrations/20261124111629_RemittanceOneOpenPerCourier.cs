using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemittanceOneOpenPerCourier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "cash_remittances_one_open_per_courier",
                table: "cash_remittances",
                column: "courierId",
                unique: true,
                filter: "\"status\" = 'DRAFT' AND \"courierClosedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "cash_remittances_one_open_per_courier",
                table: "cash_remittances");
        }
    }
}
