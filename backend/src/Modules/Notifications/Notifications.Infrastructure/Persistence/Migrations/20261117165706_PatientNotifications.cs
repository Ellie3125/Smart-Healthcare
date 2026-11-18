using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientNotification",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientNotification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientNotificationRead",
                schema: "workflow",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientNotificationRead", x => new { x.NotificationId, x.AppUserId });
                    table.ForeignKey(
                        name: "FK_PatientNotificationRead_PatientNotification_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "workflow",
                        principalTable: "PatientNotification",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientNotification_PatientId_CreatedAtUtc_Id",
                schema: "workflow",
                table: "PatientNotification",
                columns: new[] { "PatientId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_PatientNotification_SourceEventId_PatientId_Kind",
                schema: "workflow",
                table: "PatientNotification",
                columns: new[] { "SourceEventId", "PatientId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientNotificationRead",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "PatientNotification",
                schema: "workflow");
        }
    }
}
