using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthcare.Scheduling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_ErdV22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scheduling");

            migrationBuilder.CreateTable(
                name: "DoctorSessionSlotQuota",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    HeldCount = table.Column<int>(type: "int", nullable: false),
                    AdmittedCount = table.Column<int>(type: "int", nullable: false),
                    LockedCancelledCount = table.Column<int>(type: "int", nullable: false),
                    SessionStartTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BookingCutoffTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RescheduleCutoffTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckInDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SessionStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSessionSlotQuota", x => x.Id);
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_AdmittedCount", "[AdmittedCount] >= 0");
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_Capacity", "[HeldCount] + [AdmittedCount] + [LockedCancelledCount] <= [Capacity]");
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_HeldCount", "[HeldCount] >= 0");
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_LockedCancelledCount", "[LockedCancelledCount] >= 0");
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_SessionStatus", "[SessionStatus] IN (N'Scheduled', N'InProgress', N'Completed', N'Cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "ServiceStation",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reservation",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionQuotaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingChannel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservation", x => x.Id);
                    table.CheckConstraint("CK_Reservation_BookingChannel", "[BookingChannel] IN (N'Web', N'App', N'Chatbot', N'Counter')");
                    table.CheckConstraint("CK_Reservation_Status", "[Status] IN (N'Held', N'Confirmed', N'Expired', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_Reservation_DoctorSessionSlotQuota_SessionQuotaId",
                        column: x => x.SessionQuotaId,
                        principalSchema: "scheduling",
                        principalTable: "DoctorSessionSlotQuota",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QueueTicket",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TicketNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TicketType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    QueueStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ServedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueTicket", x => x.Id);
                    table.CheckConstraint("CK_QueueTicket_QueueStatus", "[QueueStatus] IN (N'Waiting', N'Called', N'Deferred', N'InProgress', N'Finished', N'Cancelled')");
                    table.CheckConstraint("CK_QueueTicket_TicketType", "[TicketType] IN (N'InitialConsultation', N'CLS', N'ReturnConsultation')");
                    table.ForeignKey(
                        name: "FK_QueueTicket_ServiceStation_TargetStationId",
                        column: x => x.TargetStationId,
                        principalSchema: "scheduling",
                        principalTable: "ServiceStation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appointment",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionQuotaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RescheduledFromAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AppliedConsultationFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointment", x => x.Id);
                    table.CheckConstraint("CK_Appointment_Status", "[Status] IN (N'Scheduled', N'CheckedIn', N'Completed', N'Cancelled', N'Absent', N'Rescheduled')");
                    table.ForeignKey(
                        name: "FK_Appointment_Appointment_RescheduledFromAppointmentId",
                        column: x => x.RescheduledFromAppointmentId,
                        principalSchema: "scheduling",
                        principalTable: "Appointment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointment_DoctorSessionSlotQuota_SessionQuotaId",
                        column: x => x.SessionQuotaId,
                        principalSchema: "scheduling",
                        principalTable: "DoctorSessionSlotQuota",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointment_Reservation_ReservationId",
                        column: x => x.ReservationId,
                        principalSchema: "scheduling",
                        principalTable: "Reservation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivePatientEngagement",
                schema: "scheduling",
                columns: table => new
                {
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivePatientEngagement", x => x.PatientId);
                    table.ForeignKey(
                        name: "FK_ActivePatientEngagement_QueueTicket_CurrentTicketId",
                        column: x => x.CurrentTicketId,
                        principalSchema: "scheduling",
                        principalTable: "QueueTicket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivePatientEngagement_ServiceStation_CurrentStationId",
                        column: x => x.CurrentStationId,
                        principalSchema: "scheduling",
                        principalTable: "ServiceStation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActiveStationAllocation",
                schema: "scheduling",
                columns: table => new
                {
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveStationAllocation", x => x.StationId);
                    table.ForeignKey(
                        name: "FK_ActiveStationAllocation_QueueTicket_CurrentTicketId",
                        column: x => x.CurrentTicketId,
                        principalSchema: "scheduling",
                        principalTable: "QueueTicket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActiveStationAllocation_ServiceStation_StationId",
                        column: x => x.StationId,
                        principalSchema: "scheduling",
                        principalTable: "ServiceStation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActiveBookingClaim",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionQuotaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveBookingClaim", x => x.Id);
                    table.CheckConstraint("CK_ActiveBookingClaim_ClaimSource", "[ClaimSource] IN (N'App', N'Web', N'Chatbot', N'Counter')");
                    table.ForeignKey(
                        name: "FK_ActiveBookingClaim_Appointment_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "scheduling",
                        principalTable: "Appointment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActiveBookingClaim_DoctorSessionSlotQuota_SessionQuotaId",
                        column: x => x.SessionQuotaId,
                        principalSchema: "scheduling",
                        principalTable: "DoctorSessionSlotQuota",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActiveBookingClaim_Reservation_ReservationId",
                        column: x => x.ReservationId,
                        principalSchema: "scheduling",
                        principalTable: "Reservation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_AppointmentId",
                schema: "scheduling",
                table: "ActiveBookingClaim",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_DoctorId",
                schema: "scheduling",
                table: "ActiveBookingClaim",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_DoctorId_ServiceDate_PatientId",
                schema: "scheduling",
                table: "ActiveBookingClaim",
                columns: new[] { "DoctorId", "ServiceDate", "PatientId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_PatientId",
                schema: "scheduling",
                table: "ActiveBookingClaim",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_ReservationId",
                schema: "scheduling",
                table: "ActiveBookingClaim",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_SessionQuotaId",
                schema: "scheduling",
                table: "ActiveBookingClaim",
                column: "SessionQuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivePatientEngagement_CurrentStationId",
                schema: "scheduling",
                table: "ActivePatientEngagement",
                column: "CurrentStationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivePatientEngagement_CurrentTicketId",
                schema: "scheduling",
                table: "ActivePatientEngagement",
                column: "CurrentTicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivePatientEngagement_EncounterId",
                schema: "scheduling",
                table: "ActivePatientEngagement",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveStationAllocation_CurrentPatientId",
                schema: "scheduling",
                table: "ActiveStationAllocation",
                column: "CurrentPatientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActiveStationAllocation_CurrentTicketId",
                schema: "scheduling",
                table: "ActiveStationAllocation",
                column: "CurrentTicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_AppointmentNumber",
                schema: "scheduling",
                table: "Appointment",
                column: "AppointmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_PatientId",
                schema: "scheduling",
                table: "Appointment",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_RescheduledFromAppointmentId",
                schema: "scheduling",
                table: "Appointment",
                column: "RescheduledFromAppointmentId",
                unique: true,
                filter: "[RescheduledFromAppointmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_ReservationId",
                schema: "scheduling",
                table: "Appointment",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_SessionQuotaId",
                schema: "scheduling",
                table: "Appointment",
                column: "SessionQuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSessionSlotQuota_AssignedRoomId",
                schema: "scheduling",
                table: "DoctorSessionSlotQuota",
                column: "AssignedRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSessionSlotQuota_DoctorId_ServiceDate",
                schema: "scheduling",
                table: "DoctorSessionSlotQuota",
                columns: new[] { "DoctorId", "ServiceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_EncounterId",
                schema: "scheduling",
                table: "QueueTicket",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_ServiceOrderId",
                schema: "scheduling",
                table: "QueueTicket",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_ServiceOrderItemId",
                schema: "scheduling",
                table: "QueueTicket",
                column: "ServiceOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_TargetRoomId",
                schema: "scheduling",
                table: "QueueTicket",
                column: "TargetRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_TargetStationId",
                schema: "scheduling",
                table: "QueueTicket",
                column: "TargetStationId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_AppUserId",
                schema: "scheduling",
                table: "Reservation",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_BookingAttemptId",
                schema: "scheduling",
                table: "Reservation",
                column: "BookingAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_IdempotencyKey",
                schema: "scheduling",
                table: "Reservation",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_PatientId",
                schema: "scheduling",
                table: "Reservation",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_SessionQuotaId",
                schema: "scheduling",
                table: "Reservation",
                column: "SessionQuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStation_RoomId",
                schema: "scheduling",
                table: "ServiceStation",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStation_StationCode",
                schema: "scheduling",
                table: "ServiceStation",
                column: "StationCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveBookingClaim",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "ActivePatientEngagement",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "ActiveStationAllocation",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "Appointment",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "QueueTicket",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "Reservation",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "ServiceStation",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "DoctorSessionSlotQuota",
                schema: "scheduling");
        }
    }
}
