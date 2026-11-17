using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QueueTicketActivationAndSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueTicket_TargetRoomId",
                schema: "scheduling",
                table: "QueueTicket");

            migrationBuilder.DropIndex(
                name: "IX_Appointment_SessionQuotaId",
                schema: "scheduling",
                table: "Appointment");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAtUtc",
                schema: "scheduling",
                table: "QueueTicket",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QueuedAtUtc",
                schema: "scheduling",
                table: "QueueTicket",
                type: "datetime2",
                nullable: false,
                computedColumnSql: "COALESCE([ActivatedAtUtc], [IssuedAtUtc])",
                stored: true);

            migrationBuilder.CreateTable(
                name: "TicketSequence",
                schema: "scheduling",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSequence", x => x.Scope);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_Room_QueuedAt",
                schema: "scheduling",
                table: "QueueTicket",
                columns: new[] { "TargetRoomId", "QueuedAtUtc" })
                .Annotation("SqlServer:Include", new[] { "QueueStatus", "TicketType", "SequenceNumber", "TicketNumber", "EncounterId", "TargetStationId", "CalledAtUtc", "ServedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_SessionQuotaId",
                schema: "scheduling",
                table: "Appointment",
                column: "SessionQuotaId")
                .Annotation("SqlServer:Include", new[] { "AppointmentNumber", "PatientId", "Status", "ConfirmedAtUtc" });

            // Điền bộ đếm từ vé đã có: Scope là tiền tố số vé (bỏ phần số thứ tự cuối, D3 hoặc dài hơn khi >= 1000).
            // Chạy lặp lại an toàn: chỉ thêm Scope chưa có. Rollback: Down xóa bảng và cột, code cũ lại dùng MAX theo tiền tố.
            migrationBuilder.Sql("""
                INSERT INTO scheduling.TicketSequence (Scope, LastValue)
                SELECT p.Scope, MAX(t.SequenceNumber)
                FROM scheduling.QueueTicket t
                CROSS APPLY (SELECT LEFT(t.TicketNumber, LEN(t.TicketNumber)
                    - IIF(t.SequenceNumber < 1000, 3, LEN(CAST(t.SequenceNumber AS varchar(11))))) AS Scope) p
                WHERE LEN(p.Scope) <= 64
                  AND NOT EXISTS (SELECT 1 FROM scheduling.TicketSequence s WHERE s.Scope = p.Scope)
                GROUP BY p.Scope;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketSequence",
                schema: "scheduling");

            migrationBuilder.DropIndex(
                name: "IX_QueueTicket_Room_QueuedAt",
                schema: "scheduling",
                table: "QueueTicket");

            migrationBuilder.DropIndex(
                name: "IX_Appointment_SessionQuotaId",
                schema: "scheduling",
                table: "Appointment");

            migrationBuilder.DropColumn(
                name: "QueuedAtUtc",
                schema: "scheduling",
                table: "QueueTicket");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                schema: "scheduling",
                table: "QueueTicket");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_TargetRoomId",
                schema: "scheduling",
                table: "QueueTicket",
                column: "TargetRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_SessionQuotaId",
                schema: "scheduling",
                table: "Appointment",
                column: "SessionQuotaId");
        }
    }
}
