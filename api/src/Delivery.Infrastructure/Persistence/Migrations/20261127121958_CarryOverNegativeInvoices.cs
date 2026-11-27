using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CarryOverNegativeInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancelReason",
                table: "payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelledAt",
                table: "payments",
                type: "timestamp(3) with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelledById",
                table: "payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carriedIntoId",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carriedFromInvoiceId",
                table: "invoice_adjustments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isInternal",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_carriedIntoId",
                table: "invoices",
                column: "carriedIntoId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_adjustments_carriedFromInvoiceId",
                table: "invoice_adjustments",
                column: "carriedFromInvoiceId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_adjustments_invoices_carriedFromInvoiceId",
                table: "invoice_adjustments",
                column: "carriedFromInvoiceId",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_invoices_carriedIntoId",
                table: "invoices",
                column: "carriedIntoId",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoice_adjustments_invoices_carriedFromInvoiceId",
                table: "invoice_adjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_invoices_carriedIntoId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_carriedIntoId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoice_adjustments_carriedFromInvoiceId",
                table: "invoice_adjustments");

            migrationBuilder.DropColumn(
                name: "cancelReason",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "cancelledAt",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "cancelledById",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "carriedIntoId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "carriedFromInvoiceId",
                table: "invoice_adjustments");

            migrationBuilder.DropColumn(
                name: "isInternal",
                table: "clients");
        }
    }
}
