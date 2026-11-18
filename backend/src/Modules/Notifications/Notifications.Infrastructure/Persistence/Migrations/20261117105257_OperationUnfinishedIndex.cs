using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperationUnfinishedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Operation_Unfinished",
                schema: "workflow",
                table: "Operation",
                columns: new[] { "Status", "CreatedAtUtc" },
                filter: "[Status] IN (N'Pending', N'Running', N'WaitingExternal', N'Failed', N'NeedsAction', N'ManualReview')")
                .Annotation("SqlServer:Include", new[] { "OperationType", "TargetEntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Operation_Unfinished",
                schema: "workflow",
                table: "Operation");
        }
    }
}
