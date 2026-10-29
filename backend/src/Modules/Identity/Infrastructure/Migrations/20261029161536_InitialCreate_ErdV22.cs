using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthcare.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_ErdV22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "AppUser",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Patient",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patient", x => x.Id);
                    table.CheckConstraint("CK_Patient_Gender", "[Gender] IN (N'Male', N'Female', N'Other')");
                });

            migrationBuilder.CreateTable(
                name: "Room",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RoomName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RoomType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Room", x => x.Id);
                    table.CheckConstraint("CK_Room_RoomType", "[RoomType] IN (N'Clinical', N'Laboratory', N'XRay', N'Ultrasound', N'Endoscopy')");
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StaffType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                    table.CheckConstraint("CK_Staff_StaffType", "[StaffType] IN (N'Doctor', N'Nurse', N'Receptionist', N'Technician')");
                    table.ForeignKey(
                        name: "FK_Staff_AppUser_AppUserId",
                        column: x => x.AppUserId,
                        principalSchema: "identity",
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientRepresentative",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AuthorizationProofDoc = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientRepresentative", x => x.Id);
                    table.CheckConstraint("CK_PatientRepresentative_Relationship", "[Relationship] IN (N'Self', N'Child', N'Parent', N'Spouse', N'Other')");
                    table.CheckConstraint("CK_PatientRepresentative_VerificationStatus", "[VerificationStatus] IN (N'Pending', N'Verified', N'Rejected', N'Revoked')");
                    table.ForeignKey(
                        name: "FK_PatientRepresentative_AppUser_AppUserId",
                        column: x => x.AppUserId,
                        principalSchema: "identity",
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientRepresentative_Patient_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "identity",
                        principalTable: "Patient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomAccount",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomAccount_AppUser_AppUserId",
                        column: x => x.AppUserId,
                        principalSchema: "identity",
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomAccount_Room_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "identity",
                        principalTable: "Room",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Doctor",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorRankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Biography = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Doctor_Staff_StaffId",
                        column: x => x.StaffId,
                        principalSchema: "identity",
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffShiftAssignment",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftName = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsShiftLeader = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffShiftAssignment", x => x.Id);
                    table.CheckConstraint("CK_StaffShiftAssignment_ShiftName", "[ShiftName] IN (N'Morning', N'Afternoon', N'FullDay')");
                    table.ForeignKey(
                        name: "FK_StaffShiftAssignment_Room_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "identity",
                        principalTable: "Room",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffShiftAssignment_Staff_StaffId",
                        column: x => x.StaffId,
                        principalSchema: "identity",
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomDeviceSession",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DeviceIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    SessionToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LogoutAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomDeviceSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomDeviceSession_RoomAccount_RoomAccountId",
                        column: x => x.RoomAccountId,
                        principalSchema: "identity",
                        principalTable: "RoomAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_Email",
                schema: "identity",
                table: "AppUser",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_PhoneNumber",
                schema: "identity",
                table: "AppUser",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_UserName",
                schema: "identity",
                table: "AppUser",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctor_DoctorRankId",
                schema: "identity",
                table: "Doctor",
                column: "DoctorRankId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctor_SpecialtyId",
                schema: "identity",
                table: "Doctor",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctor_StaffId",
                schema: "identity",
                table: "Doctor",
                column: "StaffId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patient_NationalId",
                schema: "identity",
                table: "Patient",
                column: "NationalId",
                unique: true,
                filter: "[NationalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PatientRepresentative_AppUserId",
                schema: "identity",
                table: "PatientRepresentative",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientRepresentative_PatientId",
                schema: "identity",
                table: "PatientRepresentative",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Room_RoomNumber",
                schema: "identity",
                table: "Room",
                column: "RoomNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccount_AccountCode",
                schema: "identity",
                table: "RoomAccount",
                column: "AccountCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccount_AppUserId",
                schema: "identity",
                table: "RoomAccount",
                column: "AppUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccount_RoomId",
                schema: "identity",
                table: "RoomAccount",
                column: "RoomId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomDeviceSession_RoomAccountId",
                schema: "identity",
                table: "RoomDeviceSession",
                column: "RoomAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomDeviceSession_SessionToken",
                schema: "identity",
                table: "RoomDeviceSession",
                column: "SessionToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_AppUserId",
                schema: "identity",
                table: "Staff",
                column: "AppUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_StaffCode",
                schema: "identity",
                table: "Staff",
                column: "StaffCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffShiftAssignment_RoomId",
                schema: "identity",
                table: "StaffShiftAssignment",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffShiftAssignment_StaffId",
                schema: "identity",
                table: "StaffShiftAssignment",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Doctor",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PatientRepresentative",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "RoomDeviceSession",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "StaffShiftAssignment",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Patient",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "RoomAccount",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Staff",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Room",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AppUser",
                schema: "identity");
        }
    }
}
