using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueTicketUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueTicket_ServiceOrderItemId",
                schema: "scheduling",
                table: "QueueTicket");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_ServiceOrderItemId",
                schema: "scheduling",
                table: "QueueTicket",
                column: "ServiceOrderItemId",
                unique: true,
                filter: "[ServiceOrderItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_TicketNumber",
                schema: "scheduling",
                table: "QueueTicket",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_QueueTicket_InitialPerEncounter",
                schema: "scheduling",
                table: "QueueTicket",
                column: "EncounterId",
                unique: true,
                filter: "[TicketType] = 'InitialConsultation'");

            migrationBuilder.CreateIndex(
                name: "UX_QueueTicket_ReturnPerServiceOrder",
                schema: "scheduling",
                table: "QueueTicket",
                column: "ServiceOrderId",
                unique: true,
                filter: "[TicketType] = 'ReturnConsultation'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueTicket_ServiceOrderItemId",
                schema: "scheduling",
                table: "QueueTicket");

            migrationBuilder.DropIndex(
                name: "IX_QueueTicket_TicketNumber",
                schema: "scheduling",
                table: "QueueTicket");

            migrationBuilder.DropIndex(
                name: "UX_QueueTicket_InitialPerEncounter",
                schema: "scheduling",
                table: "QueueTicket");

            migrationBuilder.DropIndex(
                name: "UX_QueueTicket_ReturnPerServiceOrder",
                schema: "scheduling",
                table: "QueueTicket");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_ServiceOrderItemId",
                schema: "scheduling",
                table: "QueueTicket",
                column: "ServiceOrderItemId");
        }
    }
}
