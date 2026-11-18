using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPatients.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignIdentityWithErd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Doctor_Room_DefaultRoomId",
                schema: "identity",
                table: "Doctor");

            migrationBuilder.DropIndex(
                name: "IX_StaffShiftAssignment_RoomId_ShiftDate_ShiftName",
                schema: "identity",
                table: "StaffShiftAssignment");

            migrationBuilder.DropIndex(
                name: "IX_StaffShiftAssignment_StaffId_ShiftDate",
                schema: "identity",
                table: "StaffShiftAssignment");

            migrationBuilder.DropIndex(
                name: "IX_RoomDeviceSession_RoomAccountId",
                schema: "identity",
                table: "RoomDeviceSession");

            migrationBuilder.DropIndex(
                name: "IX_PatientRepresentative_AppUserId",
                schema: "identity",
                table: "PatientRepresentative");

            migrationBuilder.DropIndex(
                name: "IX_Patient_OwnerUserId",
                schema: "identity",
                table: "Patient");

            migrationBuilder.DropIndex(
                name: "IX_Doctor_DefaultRoomId",
                schema: "identity",
                table: "Doctor");

            migrationBuilder.DropIndex(
                name: "IX_AppUser_Role",
                schema: "identity",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                schema: "identity",
                table: "Patient");

            migrationBuilder.DropColumn(
                name: "DefaultRoomId",
                schema: "identity",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                schema: "identity",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "identity",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "RoomId",
                schema: "identity",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "identity",
                table: "AppUser");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "identity",
                table: "AppUser",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

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

            migrationBuilder.CreateIndex(
                name: "IX_RoomDeviceSession_RoomAccountId",
                schema: "identity",
                table: "RoomDeviceSession",
                column: "RoomAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffShiftAssignment_RoomId",
                schema: "identity",
                table: "StaffShiftAssignment");

            migrationBuilder.DropIndex(
                name: "IX_StaffShiftAssignment_StaffId",
                schema: "identity",
                table: "StaffShiftAssignment");

            migrationBuilder.DropIndex(
                name: "IX_RoomDeviceSession_RoomAccountId",
                schema: "identity",
                table: "RoomDeviceSession");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                schema: "identity",
                table: "Patient",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultRoomId",
                schema: "identity",
                table: "Doctor",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "identity",
                table: "AppUser",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<Guid>(
                name: "DoctorId",
                schema: "identity",
                table: "AppUser",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "identity",
                table: "AppUser",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                schema: "identity",
                table: "AppUser",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "identity",
                table: "AppUser",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_StaffShiftAssignment_RoomId_ShiftDate_ShiftName",
                schema: "identity",
                table: "StaffShiftAssignment",
                columns: new[] { "RoomId", "ShiftDate", "ShiftName" },
                unique: true,
                filter: "[IsShiftLeader] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StaffShiftAssignment_StaffId_ShiftDate",
                schema: "identity",
                table: "StaffShiftAssignment",
                columns: new[] { "StaffId", "ShiftDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomDeviceSession_RoomAccountId",
                schema: "identity",
                table: "RoomDeviceSession",
                column: "RoomAccountId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PatientRepresentative_AppUserId",
                schema: "identity",
                table: "PatientRepresentative",
                column: "AppUserId",
                unique: true,
                filter: "[Relationship] = 'Self' AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_OwnerUserId",
                schema: "identity",
                table: "Patient",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctor_DefaultRoomId",
                schema: "identity",
                table: "Doctor",
                column: "DefaultRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_Role",
                schema: "identity",
                table: "AppUser",
                column: "Role");

            migrationBuilder.AddForeignKey(
                name: "FK_Doctor_Room_DefaultRoomId",
                schema: "identity",
                table: "Doctor",
                column: "DefaultRoomId",
                principalSchema: "identity",
                principalTable: "Room",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
