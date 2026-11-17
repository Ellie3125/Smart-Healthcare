using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClsBatchToBillingItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PatientId",
                schema: "billing",
                table: "BillingItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceOrderId",
                schema: "billing",
                table: "BillingItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingItem_PatientId_CreatedAtUtc",
                schema: "billing",
                table: "BillingItem",
                columns: new[] { "PatientId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingItem_ServiceOrderId",
                schema: "billing",
                table: "BillingItem",
                column: "ServiceOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BillingItem_PatientId_CreatedAtUtc",
                schema: "billing",
                table: "BillingItem");

            migrationBuilder.DropIndex(
                name: "IX_BillingItem_ServiceOrderId",
                schema: "billing",
                table: "BillingItem");

            migrationBuilder.DropColumn(
                name: "PatientId",
                schema: "billing",
                table: "BillingItem");

            migrationBuilder.DropColumn(
                name: "ServiceOrderId",
                schema: "billing",
                table: "BillingItem");
        }
    }
}
