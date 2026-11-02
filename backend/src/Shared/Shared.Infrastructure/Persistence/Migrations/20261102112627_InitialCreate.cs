using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shared");

            migrationBuilder.CreateTable(
                name: "ApiIdempotencyRecord",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CallerScope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBodyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiIdempotencyRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoomAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoomDeviceSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StaffShiftAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClientIpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ChangedColumns = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedEvent",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandlerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedEvent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiIdempotencyRecord_CallerScope_OperationType_IdempotencyKey",
                schema: "shared",
                table: "ApiIdempotencyRecord",
                columns: new[] { "CallerScope", "OperationType", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiIdempotencyRecord_OperationId",
                schema: "shared",
                table: "ApiIdempotencyRecord",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_OperationId",
                schema: "shared",
                table: "AuditLog",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_PerformedByUserId",
                schema: "shared",
                table: "AuditLog",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_RoomAccountId",
                schema: "shared",
                table: "AuditLog",
                column: "RoomAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_RoomDeviceSessionId",
                schema: "shared",
                table: "AuditLog",
                column: "RoomDeviceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_StaffShiftAssignmentId",
                schema: "shared",
                table: "AuditLog",
                column: "StaffShiftAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OperationId",
                schema: "shared",
                table: "OutboxMessage",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEvent_EventId_HandlerName",
                schema: "shared",
                table: "ProcessedEvent",
                columns: new[] { "EventId", "HandlerName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiIdempotencyRecord",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "ProcessedEvent",
                schema: "shared");
        }
    }
}
