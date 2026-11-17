using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OutboxUnfinishedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Unfinished",
                schema: "shared",
                table: "OutboxMessage",
                columns: new[] { "Status", "CreatedAtUtc" },
                filter: "[Status] IN (N'Pending', N'Processing', N'DeadLetter')")
                .Annotation("SqlServer:Include", new[] { "EventType", "RetryCount", "ProcessedAtUtc", "AggregateId", "OperationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_Unfinished",
                schema: "shared",
                table: "OutboxMessage");
        }
    }
}
