using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddErdV24BillingChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_RefundRequest_PolicyRule",
                schema: "billing",
                table: "RefundRequest",
                sql: "([RefundReason] = 'CancelledBefore0500' AND [PolicyRuleApplied] = 'FullRefundPre0500') OR ([RefundReason] <> 'CancelledBefore0500' AND [PolicyRuleApplied] = 'CustomApproval')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BillingItem_TotalAmount",
                schema: "billing",
                table: "BillingItem",
                sql: "[TotalAmount] = [UnitPrice] * [Quantity]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RefundRequest_PolicyRule",
                schema: "billing",
                table: "RefundRequest");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BillingItem_TotalAmount",
                schema: "billing",
                table: "BillingItem");
        }
    }
}
