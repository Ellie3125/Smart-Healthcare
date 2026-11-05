using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPatients.Infrastructure.Persistence.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(IdentityPatientsDbContext))]
[Migration("20261105000000_InitialIdentity")]
public partial class InitialIdentity : Migration
{
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
                UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppUser", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DoctorRank",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RankCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                RankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DoctorRank", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Patient",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                Gender = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                NationalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Patient", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Room",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoomNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                RoomName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RoomType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                DepartmentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Room", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Specialty",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SpecialtyCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                SpecialtyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Specialty", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Staff",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StaffCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                StaffType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Staff", x => x.Id);
                table.ForeignKey(
                    name: "FK_Staff_AppUser_AppUserId",
                    column: x => x.AppUserId,
                    principalSchema: "identity",
                    principalTable: "AppUser",
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
                DefaultRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Biography = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Doctor", x => x.Id);
                table.ForeignKey(
                    name: "FK_Doctor_DoctorRank_DoctorRankId",
                    column: x => x.DoctorRankId,
                    principalSchema: "identity",
                    principalTable: "DoctorRank",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Doctor_Room_DefaultRoomId",
                    column: x => x.DefaultRoomId,
                    principalSchema: "identity",
                    principalTable: "Room",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Doctor_Specialty_SpecialtyId",
                    column: x => x.SpecialtyId,
                    principalSchema: "identity",
                    principalTable: "Specialty",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Doctor_Staff_StaffId",
                    column: x => x.StaffId,
                    principalSchema: "identity",
                    principalTable: "Staff",
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
                AccountCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
            name: "PatientRepresentative",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Relationship = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                VerificationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PatientRepresentative", x => x.Id);
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

        // Indexes
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
            name: "IX_AppUser_Role",
            schema: "identity",
            table: "AppUser",
            column: "Role");

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
            name: "IX_DoctorRank_RankCode",
            schema: "identity",
            table: "DoctorRank",
            column: "RankCode",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Patient_NationalId",
            schema: "identity",
            table: "Patient",
            column: "NationalId",
            unique: true,
            filter: "[NationalId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Patient_OwnerUserId",
            schema: "identity",
            table: "Patient",
            column: "OwnerUserId");

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
            name: "IX_Specialty_SpecialtyCode",
            schema: "identity",
            table: "Specialty",
            column: "SpecialtyCode",
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
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Doctor", schema: "identity");
        migrationBuilder.DropTable(name: "PatientRepresentative", schema: "identity");
        migrationBuilder.DropTable(name: "RoomAccount", schema: "identity");
        migrationBuilder.DropTable(name: "Staff", schema: "identity");
        migrationBuilder.DropTable(name: "DoctorRank", schema: "identity");
        migrationBuilder.DropTable(name: "Patient", schema: "identity");
        migrationBuilder.DropTable(name: "Room", schema: "identity");
        migrationBuilder.DropTable(name: "Specialty", schema: "identity");
        migrationBuilder.DropTable(name: "AppUser", schema: "identity");
    }
}

