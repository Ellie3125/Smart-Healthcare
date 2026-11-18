using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierShiftClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashierShiftClosure",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashierUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceiptCount = table.Column<int>(type: "int", nullable: false),
                    RefundCount = table.Column<int>(type: "int", nullable: false),
                    ExpectedCashCollected = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCashRefunded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualCashCounted = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DiscrepancyNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierShiftClosure", x => x.Id);
                    table.CheckConstraint("CK_CashierShiftClosure_Amounts", "[ExpectedCashCollected] >= 0 AND [ExpectedCashRefunded] >= 0 AND [ActualCashCounted] >= 0 AND [ReceiptCount] >= 0 AND [RefundCount] >= 0");
                    table.CheckConstraint("CK_CashierShiftClosure_DiscrepancyNote", "[ActualCashCounted] = [ExpectedCashCollected] - [ExpectedCashRefunded] OR ([DiscrepancyNote] IS NOT NULL AND LEN(LTRIM(RTRIM([DiscrepancyNote]))) > 0)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashierShiftClosure_CashierUserId_ShiftDate",
                schema: "billing",
                table: "CashierShiftClosure",
                columns: new[] { "CashierUserId", "ShiftDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashierShiftClosure",
                schema: "billing");
        }
    }
}
