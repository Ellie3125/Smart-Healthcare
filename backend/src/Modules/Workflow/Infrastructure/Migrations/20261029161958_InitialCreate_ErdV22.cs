using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthcare.Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_ErdV22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow");

            migrationBuilder.CreateTable(
                name: "BookingAttempt",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedDoctorRankId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentStep = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SelectedServiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingAttempt", x => x.Id);
                    table.CheckConstraint("CK_BookingAttempt_CurrentStep", "[CurrentStep] IN (N'ProfileSelection', N'DoctorSelection', N'SlotHeld', N'PaymentInitiated', N'Finalized', N'Abandoned')");
                    table.CheckConstraint("CK_BookingAttempt_Status", "[Status] IN (N'Draft', N'Held', N'WaitingPayment', N'Confirming', N'Confirmed', N'NeedsReselection', N'PaidUnallocated', N'Cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "Operation",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TargetEntityType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operation", x => x.Id);
                    table.CheckConstraint("CK_Operation_OperationType", "[OperationType] IN (N'HoldSlot', N'ConfirmBooking', N'RescheduleBooking', N'CancelBooking', N'AllocateStation', N'ApproveResult', N'ProcessRefund')");
                    table.CheckConstraint("CK_Operation_Status", "[Status] IN (N'Pending', N'Running', N'WaitingExternal', N'Succeeded', N'Failed', N'NeedsAction', N'ManualReview')");
                    table.CheckConstraint("CK_Operation_TargetEntityType", "[TargetEntityType] IN (N'BookingAttempt', N'Reservation', N'Appointment', N'BillingItem', N'QueueTicket', N'CLSResult')");
                    table.ForeignKey(
                        name: "FK_Operation_BookingAttempt_BookingAttemptId",
                        column: x => x.BookingAttemptId,
                        principalSchema: "workflow",
                        principalTable: "BookingAttempt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApiIdempotencyRecord",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CallerScope = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBodyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiIdempotencyRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiIdempotencyRecord_Operation_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "workflow",
                        principalTable: "Operation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoomAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoomDeviceSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StaffShiftAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ClientIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ChangedColumns = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OldValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                    table.CheckConstraint("CK_AuditLog_ActionType", "[ActionType] IN (N'INSERT', N'UPDATE', N'DELETE', N'CANCEL', N'REFUND', N'PUBLISH')");
                    table.ForeignKey(
                        name: "FK_AuditLog_Operation_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "workflow",
                        principalTable: "Operation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "workflow",
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
                    table.CheckConstraint("CK_OutboxMessage_AggregateType", "[AggregateType] IN (N'Reservation', N'Appointment', N'Payment', N'CLSResult')");
                    table.CheckConstraint("CK_OutboxMessage_EventType", "[EventType] IN (N'SlotHeldEvent', N'BookingConfirmedEvent', N'PaymentSettledEvent', N'CLSResultPublishedEvent')");
                    table.CheckConstraint("CK_OutboxMessage_Status", "[Status] IN (N'Pending', N'Processing', N'Published', N'DeadLetter')");
                    table.ForeignKey(
                        name: "FK_OutboxMessage_Operation_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "workflow",
                        principalTable: "Operation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiIdempotencyRecord_OperationId",
                schema: "workflow",
                table: "ApiIdempotencyRecord",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "UK_Idem",
                schema: "workflow",
                table: "ApiIdempotencyRecord",
                columns: new[] { "CallerScope", "OperationType", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_OperationId",
                schema: "workflow",
                table: "AuditLog",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_PerformedByUserId",
                schema: "workflow",
                table: "AuditLog",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_RoomAccountId",
                schema: "workflow",
                table: "AuditLog",
                column: "RoomAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_RoomDeviceSessionId",
                schema: "workflow",
                table: "AuditLog",
                column: "RoomDeviceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_StaffShiftAssignmentId",
                schema: "workflow",
                table: "AuditLog",
                column: "StaffShiftAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAttempt_AppUserId",
                schema: "workflow",
                table: "BookingAttempt",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAttempt_PatientId",
                schema: "workflow",
                table: "BookingAttempt",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAttempt_SelectedDoctorId",
                schema: "workflow",
                table: "BookingAttempt",
                column: "SelectedDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAttempt_SelectedDoctorRankId",
                schema: "workflow",
                table: "BookingAttempt",
                column: "SelectedDoctorRankId");

            migrationBuilder.CreateIndex(
                name: "IX_Operation_BookingAttemptId",
                schema: "workflow",
                table: "Operation",
                column: "BookingAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_Operation_InitiatedByUserId",
                schema: "workflow",
                table: "Operation",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OperationId",
                schema: "workflow",
                table: "OutboxMessage",
                column: "OperationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiIdempotencyRecord",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "Operation",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "BookingAttempt",
                schema: "workflow");
        }
    }
}
