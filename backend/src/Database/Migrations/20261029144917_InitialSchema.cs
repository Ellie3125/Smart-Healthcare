using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthcare.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "AppUser",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_AppUser_Id"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_AppUser_CreatedAtUtc"),
                    Email = table.Column<string>(type: "nvarchar(256)", nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_AppUser_EmailConfirmed"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_AppUser_IsActive"),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(15)", nullable: false),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_AppUser_PhoneNumberConfirmed"),
                    UserName = table.Column<string>(type: "nvarchar(50)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUser", x => x.Id);
                    table.UniqueConstraint("UQ_AppUser_Email", x => x.Email);
                    table.UniqueConstraint("UQ_AppUser_PhoneNumber", x => x.PhoneNumber);
                    table.UniqueConstraint("UQ_AppUser_UserName", x => x.UserName);
                });

            migrationBuilder.CreateTable(
                name: "DoctorRank",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_DoctorRank_Id"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_DoctorRank_IsActive"),
                    RankCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    RankName = table.Column<string>(type: "nvarchar(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorRank", x => x.Id);
                    table.UniqueConstraint("UQ_DoctorRank_RankCode", x => x.RankCode);
                });

            migrationBuilder.CreateTable(
                name: "MedicationCatalog",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_MedicationCatalog_Id"),
                    ActiveIngredient = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    DosageForm = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_MedicationCatalog_IsActive"),
                    MedicationCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    MedicationName = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationCatalog", x => x.Id);
                    table.UniqueConstraint("UQ_MedicationCatalog_MedicationCode", x => x.MedicationCode);
                    table.CheckConstraint("CK_MedicationCatalog_DosageForm", "DosageForm IN (N'Tablet', N'Capsule', N'Liquid', N'Injection')");
                    table.CheckConstraint("CK_MedicationCatalog_Unit", "Unit IN (N'Vien', N'Goi', N'Chai', N'Ong')");
                });

            migrationBuilder.CreateTable(
                name: "Patient",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Patient_Id"),
                    Address = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Patient_CreatedAtUtc"),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    NationalId = table.Column<string>(type: "varchar(12)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "varchar(15)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patient", x => x.Id);
                    table.CheckConstraint("CK_Patient_Gender", "Gender IN (N'Male', N'Female', N'Other')");
                    table.CheckConstraint("CK_Patient_NationalId", "NationalId NOT LIKE '%[^0-9]%' AND LEN(NationalId) = 12");
                });

            migrationBuilder.CreateTable(
                name: "PriceList",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PriceList_Id"),
                    Currency = table.Column<string>(type: "char(3)", nullable: false, defaultValueSql: "'VND'")
                        .Annotation("Relational:DefaultConstraintName", "DF_PriceList_Currency"),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_PriceList_IsActive"),
                    PriceListCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    PriceListName = table.Column<string>(type: "nvarchar(150)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceList", x => x.Id);
                    table.UniqueConstraint("UQ_PriceList_PriceListCode", x => x.PriceListCode);
                    table.CheckConstraint("CK_PriceList_Currency", "Currency = 'VND'");
                    table.CheckConstraint("CK_PriceList_EffectiveRange", "EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc");
                });

            migrationBuilder.CreateTable(
                name: "Room",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Room_Id"),
                    DepartmentName = table.Column<string>(type: "nvarchar(150)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_Room_IsActive"),
                    RoomName = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    RoomNumber = table.Column<string>(type: "varchar(20)", nullable: false),
                    RoomType = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Room", x => x.Id);
                    table.UniqueConstraint("UQ_Room_RoomNumber", x => x.RoomNumber);
                    table.CheckConstraint("CK_Room_RoomType", "RoomType IN (N'Clinical', N'Laboratory', N'XRay', N'Ultrasound', N'Endoscopy')");
                });

            migrationBuilder.CreateTable(
                name: "Specialty",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Specialty_Id"),
                    Description = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_Specialty_IsActive"),
                    SpecialtyCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    SpecialtyName = table.Column<string>(type: "nvarchar(150)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specialty", x => x.Id);
                    table.UniqueConstraint("UQ_Specialty_SpecialtyCode", x => x.SpecialtyCode);
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Staff_Id"),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Staff_CreatedAtUtc"),
                    FullName = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_Staff_IsActive"),
                    StaffCode = table.Column<string>(type: "varchar(20)", nullable: false),
                    StaffType = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                    table.UniqueConstraint("UQ_Staff_AppUserId", x => x.AppUserId);
                    table.UniqueConstraint("UQ_Staff_StaffCode", x => x.StaffCode);
                    table.CheckConstraint("CK_Staff_StaffType", "StaffType IN (N'Doctor', N'Nurse', N'Receptionist', N'Technician')");
                    table.ForeignKey(
                        name: "FK_Staff_AppUser",
                        column: x => x.AppUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PatientRepresentative",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PatientRepresentative_Id"),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizationProofDoc = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PatientRepresentative_CreatedAtUtc"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_PatientRepresentative_IsActive"),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Pending'")
                        .Annotation("Relational:DefaultConstraintName", "DF_PatientRepresentative_VerificationStatus")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientRepresentative", x => x.Id);
                    table.CheckConstraint("CK_PatientRepresentative_Relationship", "Relationship IN (N'Self', N'Child', N'Parent', N'Spouse', N'Other')");
                    table.CheckConstraint("CK_PatientRepresentative_VerificationStatus", "VerificationStatus IN (N'Pending', N'Verified', N'Rejected', N'Revoked')");
                    table.ForeignKey(
                        name: "FK_PatientRepresentative_AppUser",
                        column: x => x.AppUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatientRepresentative_Patient",
                        column: x => x.PatientId,
                        principalSchema: "dbo",
                        principalTable: "Patient",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MedicalService",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_MedicalService_Id"),
                    DefaultRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_MedicalService_IsActive"),
                    IsMandatoryDefault = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_MedicalService_IsMandatoryDefault"),
                    ServiceCategory = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    ServiceCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(200)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalService", x => x.Id);
                    table.UniqueConstraint("UQ_MedicalService_ServiceCode", x => x.ServiceCode);
                    table.CheckConstraint("CK_MedicalService_ServiceCategory", "ServiceCategory IN (N'Laboratory', N'Radiology', N'Ultrasound', N'Endoscopy')");
                    table.ForeignKey(
                        name: "FK_MedicalService_Room",
                        column: x => x.DefaultRoomId,
                        principalSchema: "dbo",
                        principalTable: "Room",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RoomAccount",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RoomAccount_Id"),
                    AccountCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RoomAccount_CreatedAtUtc"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_RoomAccount_IsActive"),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomAccount", x => x.Id);
                    table.UniqueConstraint("UQ_RoomAccount_AccountCode", x => x.AccountCode);
                    table.UniqueConstraint("UQ_RoomAccount_AppUserId", x => x.AppUserId);
                    table.UniqueConstraint("UQ_RoomAccount_RoomId", x => x.RoomId);
                    table.ForeignKey(
                        name: "FK_RoomAccount_AppUser",
                        column: x => x.AppUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RoomAccount_Room",
                        column: x => x.RoomId,
                        principalSchema: "dbo",
                        principalTable: "Room",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceStation",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ServiceStation_Id"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_ServiceStation_IsActive"),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StationCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    StationName = table.Column<string>(type: "nvarchar(150)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStation", x => x.Id);
                    table.UniqueConstraint("UQ_ServiceStation_StationCode", x => x.StationCode);
                    table.ForeignKey(
                        name: "FK_ServiceStation_Room",
                        column: x => x.RoomId,
                        principalSchema: "dbo",
                        principalTable: "Room",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Doctor",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Doctor_Id"),
                    Biography = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorRankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_Doctor_IsActive"),
                    SpecialtyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctor", x => x.Id);
                    table.UniqueConstraint("UQ_Doctor_StaffId", x => x.StaffId);
                    table.ForeignKey(
                        name: "FK_Doctor_DoctorRank",
                        column: x => x.DoctorRankId,
                        principalSchema: "dbo",
                        principalTable: "DoctorRank",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Doctor_Specialty",
                        column: x => x.SpecialtyId,
                        principalSchema: "dbo",
                        principalTable: "Specialty",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Doctor_Staff",
                        column: x => x.StaffId,
                        principalSchema: "dbo",
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StaffShiftAssignment",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_StaffShiftAssignment_Id"),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IsShiftLeader = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_StaffShiftAssignment_IsShiftLeader"),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftName = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffShiftAssignment", x => x.Id);
                    table.CheckConstraint("CK_StaffShiftAssignment_Range", "EndsAtUtc > StartsAtUtc");
                    table.CheckConstraint("CK_StaffShiftAssignment_ShiftName", "ShiftName IN (N'Morning', N'Afternoon', N'FullDay')");
                    table.ForeignKey(
                        name: "FK_StaffShiftAssignment_Room",
                        column: x => x.RoomId,
                        principalSchema: "dbo",
                        principalTable: "Room",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffShiftAssignment_Staff",
                        column: x => x.StaffId,
                        principalSchema: "dbo",
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MedicalServiceRoom",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_MedicalServiceRoom_Id"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_MedicalServiceRoom_IsActive"),
                    IsDefaultRoom = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_MedicalServiceRoom_IsDefaultRoom"),
                    MedicalServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalServiceRoom", x => x.Id);
                    table.UniqueConstraint("UQ_MedicalServiceRoom_ServiceRoom", x => new { x.MedicalServiceId, x.RoomId });
                    table.ForeignKey(
                        name: "FK_MedicalServiceRoom_MedicalService",
                        column: x => x.MedicalServiceId,
                        principalSchema: "dbo",
                        principalTable: "MedicalService",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MedicalServiceRoom_Room",
                        column: x => x.RoomId,
                        principalSchema: "dbo",
                        principalTable: "Room",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RoomDeviceSession",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RoomDeviceSession_Id"),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    DeviceIpAddress = table.Column<string>(type: "varchar(45)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_RoomDeviceSession_IsActive"),
                    LoginAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    LogoutAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RoomAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionToken = table.Column<string>(type: "varchar(200)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomDeviceSession", x => x.Id);
                    table.UniqueConstraint("UQ_RoomDeviceSession_SessionToken", x => x.SessionToken);
                    table.ForeignKey(
                        name: "FK_RoomDeviceSession_RoomAccount",
                        column: x => x.RoomAccountId,
                        principalSchema: "dbo",
                        principalTable: "RoomAccount",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BookingAttempt",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_BookingAttempt_Id"),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_BookingAttempt_CreatedAtUtc"),
                    CurrentStep = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'ProfileSelection'")
                        .Annotation("Relational:DefaultConstraintName", "DF_BookingAttempt_CurrentStep"),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SelectedDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedDoctorRankId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedServiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Draft'")
                        .Annotation("Relational:DefaultConstraintName", "DF_BookingAttempt_Status"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_BookingAttempt_UpdatedAtUtc")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingAttempt", x => x.Id);
                    table.CheckConstraint("CK_BookingAttempt_CurrentStep", "CurrentStep IN (N'ProfileSelection', N'DoctorSelection', N'SlotHeld', N'PaymentInitiated', N'Finalized', N'Abandoned')");
                    table.CheckConstraint("CK_BookingAttempt_Status", "Status IN (N'Draft', N'Held', N'WaitingPayment', N'Confirming', N'Confirmed', N'NeedsReselection', N'PaidUnallocated', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_BookingAttempt_AppUser",
                        column: x => x.AppUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingAttempt_Doctor",
                        column: x => x.SelectedDoctorId,
                        principalSchema: "dbo",
                        principalTable: "Doctor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingAttempt_DoctorRank",
                        column: x => x.SelectedDoctorRankId,
                        principalSchema: "dbo",
                        principalTable: "DoctorRank",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingAttempt_Patient",
                        column: x => x.PatientId,
                        principalSchema: "dbo",
                        principalTable: "Patient",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DoctorSessionSlotQuota",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_DoctorSessionSlotQuota_Id"),
                    AdmittedCount = table.Column<int>(type: "int", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_DoctorSessionSlotQuota_AdmittedCount"),
                    AssignedRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingCutoffTimeUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false, defaultValueSql: "30")
                        .Annotation("Relational:DefaultConstraintName", "DF_DoctorSessionSlotQuota_Capacity"),
                    CheckInDeadlineUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeldCount = table.Column<int>(type: "int", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_DoctorSessionSlotQuota_HeldCount"),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_DoctorSessionSlotQuota_IsOpen"),
                    LockedCancelledCount = table.Column<int>(type: "int", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_DoctorSessionSlotQuota_LockedCancelledCount"),
                    RescheduleCutoffTimeUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionStartTimeUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    SessionStatus = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Scheduled'")
                        .Annotation("Relational:DefaultConstraintName", "DF_DoctorSessionSlotQuota_SessionStatus")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSessionSlotQuota", x => x.Id);
                    table.UniqueConstraint("UQ_DoctorSessionSlotQuota_DoctorDate", x => new { x.DoctorId, x.ServiceDate });
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_AdmittedCount", "AdmittedCount >= 0");
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_Capacity", "Capacity > 0");
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_HeldCount", "HeldCount >= 0");
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_LockedCancelledCount", "LockedCancelledCount >= 0");
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_NotOverbooked", "HeldCount + AdmittedCount + LockedCancelledCount <= Capacity");
                    table.CheckConstraint("CK_DoctorSessionSlotQuota_SessionStatus", "SessionStatus IN (N'Scheduled', N'InProgress', N'Completed', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_DoctorSessionSlotQuota_Doctor",
                        column: x => x.DoctorId,
                        principalSchema: "dbo",
                        principalTable: "Doctor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DoctorSessionSlotQuota_Room",
                        column: x => x.AssignedRoomId,
                        principalSchema: "dbo",
                        principalTable: "Room",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PriceListItem",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PriceListItem_Id"),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorRankId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_PriceListItem_IsActive"),
                    ItemType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    MedicalServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListItem", x => x.Id);
                    table.CheckConstraint("CK_PriceListItem_ItemType", "ItemType IN (N'ConsultationFee', N'CLSService')");
                    table.CheckConstraint("CK_PriceListItem_Target", "(ItemType = N'ConsultationFee' AND MedicalServiceId IS NULL\n    AND ((DoctorRankId IS NOT NULL AND DoctorId IS NULL) OR (DoctorRankId IS NULL AND DoctorId IS NOT NULL)))\nOR (ItemType = N'CLSService' AND MedicalServiceId IS NOT NULL AND DoctorRankId IS NULL AND DoctorId IS NULL)");
                    table.CheckConstraint("CK_PriceListItem_UnitPrice", "UnitPrice >= 0");
                    table.ForeignKey(
                        name: "FK_PriceListItem_Doctor",
                        column: x => x.DoctorId,
                        principalSchema: "dbo",
                        principalTable: "Doctor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PriceListItem_DoctorRank",
                        column: x => x.DoctorRankId,
                        principalSchema: "dbo",
                        principalTable: "DoctorRank",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PriceListItem_MedicalService",
                        column: x => x.MedicalServiceId,
                        principalSchema: "dbo",
                        principalTable: "MedicalService",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PriceListItem_PriceList",
                        column: x => x.PriceListId,
                        principalSchema: "dbo",
                        principalTable: "PriceList",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Operation",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Operation_Id"),
                    BookingAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Operation_CreatedAtUtc"),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    InitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Pending'")
                        .Annotation("Relational:DefaultConstraintName", "DF_Operation_Status"),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetEntityType = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operation", x => x.Id);
                    table.CheckConstraint("CK_Operation_OperationType", "OperationType IN (N'HoldSlot', N'ConfirmBooking', N'RescheduleBooking', N'CancelBooking', N'AllocateStation', N'ApproveResult', N'ProcessRefund')");
                    table.CheckConstraint("CK_Operation_Status", "Status IN (N'Pending', N'Running', N'WaitingExternal', N'Succeeded', N'Failed', N'NeedsAction', N'ManualReview')");
                    table.CheckConstraint("CK_Operation_TargetEntityType", "TargetEntityType IN (N'BookingAttempt', N'Reservation', N'Appointment', N'BillingItem', N'QueueTicket', N'CLSResult')");
                    table.ForeignKey(
                        name: "FK_Operation_AppUser",
                        column: x => x.InitiatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Operation_BookingAttempt",
                        column: x => x.BookingAttemptId,
                        principalSchema: "dbo",
                        principalTable: "BookingAttempt",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Reservation",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Reservation_Id"),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingChannel = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", nullable: true),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SessionQuotaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Held'")
                        .Annotation("Relational:DefaultConstraintName", "DF_Reservation_Status")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservation", x => x.Id);
                    table.CheckConstraint("CK_Reservation_BookingChannel", "BookingChannel IN (N'Web', N'App', N'Chatbot', N'Counter')");
                    table.CheckConstraint("CK_Reservation_Expiry", "ExpiresAtUtc > ReservedAtUtc");
                    table.CheckConstraint("CK_Reservation_Status", "Status IN (N'Held', N'Confirmed', N'Expired', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_Reservation_AppUser",
                        column: x => x.AppUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reservation_BookingAttempt",
                        column: x => x.BookingAttemptId,
                        principalSchema: "dbo",
                        principalTable: "BookingAttempt",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reservation_DoctorSessionSlotQuota",
                        column: x => x.SessionQuotaId,
                        principalSchema: "dbo",
                        principalTable: "DoctorSessionSlotQuota",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reservation_Patient",
                        column: x => x.PatientId,
                        principalSchema: "dbo",
                        principalTable: "Patient",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ApiIdempotencyRecord",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ApiIdempotencyRecord_Id"),
                    CallerScope = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ApiIdempotencyRecord_CreatedAtUtc"),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationType = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    RequestHash = table.Column<string>(type: "char(64)", nullable: false),
                    ResponseBodyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiIdempotencyRecord", x => x.Id);
                    table.UniqueConstraint("UK_Idem", x => new { x.CallerScope, x.OperationType, x.IdempotencyKey });
                    table.CheckConstraint("CK_ApiIdempotencyRecord_ResponseBodyJson", "ISJSON(ResponseBodyJson) = 1");
                    table.ForeignKey(
                        name: "FK_ApiIdempotencyRecord_Operation",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "Operation",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_AuditLog_Id"),
                    ActionType = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    ChangedColumns = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientIpAddress = table.Column<string>(type: "varchar(45)", nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoomDeviceSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StaffShiftAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TableName = table.Column<string>(type: "varchar(128)", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_AuditLog_TimestampUtc")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                    table.CheckConstraint("CK_AuditLog_ActionType", "ActionType IN (N'INSERT', N'UPDATE', N'DELETE', N'CANCEL', N'REFUND', N'PUBLISH')");
                    table.CheckConstraint("CK_AuditLog_NewValuesJson", "ISJSON(NewValuesJson) = 1");
                    table.CheckConstraint("CK_AuditLog_OldValuesJson", "ISJSON(OldValuesJson) = 1");
                    table.ForeignKey(
                        name: "FK_AuditLog_AppUser",
                        column: x => x.PerformedByUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditLog_Operation",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "Operation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditLog_RoomAccount",
                        column: x => x.RoomAccountId,
                        principalSchema: "dbo",
                        principalTable: "RoomAccount",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditLog_RoomDeviceSession",
                        column: x => x.RoomDeviceSessionId,
                        principalSchema: "dbo",
                        principalTable: "RoomDeviceSession",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditLog_StaffShiftAssignment",
                        column: x => x.StaffShiftAssignmentId,
                        principalSchema: "dbo",
                        principalTable: "StaffShiftAssignment",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_OutboxMessage_Id"),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_OutboxMessage_CreatedAtUtc"),
                    EventType = table.Column<string>(type: "nvarchar(30)", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_OutboxMessage_RetryCount"),
                    Status = table.Column<string>(type: "nvarchar(15)", nullable: false, defaultValueSql: "N'Pending'")
                        .Annotation("Relational:DefaultConstraintName", "DF_OutboxMessage_Status")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.Id);
                    table.CheckConstraint("CK_OutboxMessage_AggregateType", "AggregateType IN (N'Reservation', N'Appointment', N'Payment', N'CLSResult')");
                    table.CheckConstraint("CK_OutboxMessage_EventType", "EventType IN (N'SlotHeldEvent', N'BookingConfirmedEvent', N'PaymentSettledEvent', N'CLSResultPublishedEvent')");
                    table.CheckConstraint("CK_OutboxMessage_PayloadJson", "ISJSON(PayloadJson) = 1");
                    table.CheckConstraint("CK_OutboxMessage_RetryCount", "RetryCount >= 0");
                    table.CheckConstraint("CK_OutboxMessage_Status", "Status IN (N'Pending', N'Processing', N'Published', N'DeadLetter')");
                    table.ForeignKey(
                        name: "FK_OutboxMessage_Operation",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "Operation",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Appointment",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Appointment_Id"),
                    AppliedConsultationFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppointmentNumber = table.Column<string>(type: "varchar(30)", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RescheduledFromAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SessionQuotaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Scheduled'")
                        .Annotation("Relational:DefaultConstraintName", "DF_Appointment_Status")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointment", x => x.Id);
                    table.UniqueConstraint("UQ_Appointment_AppointmentNumber", x => x.AppointmentNumber);
                    table.UniqueConstraint("UQ_Appointment_ReservationId", x => x.ReservationId);
                    table.CheckConstraint("CK_Appointment_AppliedConsultationFee", "AppliedConsultationFee >= 0");
                    table.CheckConstraint("CK_Appointment_Status", "Status IN (N'Scheduled', N'CheckedIn', N'Completed', N'Cancelled', N'Absent')");
                    table.ForeignKey(
                        name: "FK_Appointment_DoctorSessionSlotQuota",
                        column: x => x.SessionQuotaId,
                        principalSchema: "dbo",
                        principalTable: "DoctorSessionSlotQuota",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointment_Patient",
                        column: x => x.PatientId,
                        principalSchema: "dbo",
                        principalTable: "Patient",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointment_RescheduledFrom",
                        column: x => x.RescheduledFromAppointmentId,
                        principalSchema: "dbo",
                        principalTable: "Appointment",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointment_Reservation",
                        column: x => x.ReservationId,
                        principalSchema: "dbo",
                        principalTable: "Reservation",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ActiveBookingClaim",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ActiveBookingClaim_Id"),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimSource = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ActiveBookingClaim_CreatedAtUtc"),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_ActiveBookingClaim_IsActive"),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionQuotaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveBookingClaim", x => x.Id);
                    table.CheckConstraint("CK_ActiveBookingClaim_ClaimSource", "ClaimSource IN (N'App', N'Web', N'Chatbot', N'Counter')");
                    table.ForeignKey(
                        name: "FK_ActiveBookingClaim_Appointment",
                        column: x => x.AppointmentId,
                        principalSchema: "dbo",
                        principalTable: "Appointment",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActiveBookingClaim_Doctor",
                        column: x => x.DoctorId,
                        principalSchema: "dbo",
                        principalTable: "Doctor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActiveBookingClaim_DoctorSessionSlotQuota",
                        column: x => x.SessionQuotaId,
                        principalSchema: "dbo",
                        principalTable: "DoctorSessionSlotQuota",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActiveBookingClaim_Patient",
                        column: x => x.PatientId,
                        principalSchema: "dbo",
                        principalTable: "Patient",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActiveBookingClaim_Reservation",
                        column: x => x.ReservationId,
                        principalSchema: "dbo",
                        principalTable: "Reservation",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Encounter",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Encounter_Id"),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckedInAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DoctorRankSnapshot = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    OriginalDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SpecialtySnapshot = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'InProgress'")
                        .Annotation("Relational:DefaultConstraintName", "DF_Encounter_Status")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounter", x => x.Id);
                    table.UniqueConstraint("UQ_Encounter_AppointmentId", x => x.AppointmentId);
                    table.CheckConstraint("CK_Encounter_Status", "Status IN (N'InProgress', N'WaitingCLS', N'ReturningDoctor', N'Completed', N'Abandoned')");
                    table.ForeignKey(
                        name: "FK_Encounter_Appointment",
                        column: x => x.AppointmentId,
                        principalSchema: "dbo",
                        principalTable: "Appointment",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Encounter_Doctor",
                        column: x => x.OriginalDoctorId,
                        principalSchema: "dbo",
                        principalTable: "Doctor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Encounter_Patient",
                        column: x => x.PatientId,
                        principalSchema: "dbo",
                        principalTable: "Patient",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Encounter_Room",
                        column: x => x.OriginalRoomId,
                        principalSchema: "dbo",
                        principalTable: "Room",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EncounterClinicalRecord",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_EncounterClinicalRecord_Id"),
                    ChiefComplaint = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    ClinicalExaminationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FollowUpInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitialClinicalImpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    MedicalHistory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostCLSConclusion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Draft'")
                        .Annotation("Relational:DefaultConstraintName", "DF_EncounterClinicalRecord_Status"),
                    Symptoms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TreatmentPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterClinicalRecord", x => x.Id);
                    table.UniqueConstraint("UQ_EncounterClinicalRecord_EncounterId", x => x.EncounterId);
                    table.CheckConstraint("CK_EncounterClinicalRecord_Status", "Status IN (N'Draft', N'Published')");
                    table.ForeignKey(
                        name: "FK_EncounterClinicalRecord_Doctor",
                        column: x => x.UpdatedByDoctorId,
                        principalSchema: "dbo",
                        principalTable: "Doctor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EncounterClinicalRecord_Encounter",
                        column: x => x.EncounterId,
                        principalSchema: "dbo",
                        principalTable: "Encounter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EncounterDiagnosis",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_EncounterDiagnosis_Id"),
                    ClinicalNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiagnosedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DiagnosisName = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    DiagnosisType = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ICD10Code = table.Column<string>(type: "varchar(10)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_EncounterDiagnosis_IsPrimary")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterDiagnosis", x => x.Id);
                    table.CheckConstraint("CK_EncounterDiagnosis_DiagnosisType", "DiagnosisType IN (N'Initial', N'Final')");
                    table.ForeignKey(
                        name: "FK_EncounterDiagnosis_Encounter",
                        column: x => x.EncounterId,
                        principalSchema: "dbo",
                        principalTable: "Encounter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentIntent",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentIntent_Id"),
                    BookingAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentIntent_CreatedAtUtc"),
                    Currency = table.Column<string>(type: "char(3)", nullable: false, defaultValueSql: "'VND'")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentIntent_Currency"),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IntentNumber = table.Column<string>(type: "varchar(30)", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayerAppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Pending'")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentIntent_Status"),
                    TotalExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentIntent", x => x.Id);
                    table.UniqueConstraint("UQ_PaymentIntent_IntentNumber", x => x.IntentNumber);
                    table.CheckConstraint("CK_PaymentIntent_Currency", "Currency = 'VND'");
                    table.CheckConstraint("CK_PaymentIntent_PaymentType", "PaymentType IN (N'InitialConsultation', N'CLSOrderBatch')");
                    table.CheckConstraint("CK_PaymentIntent_Provider", "Provider IN (N'VNPT', N'MoMo', N'Counter')");
                    table.CheckConstraint("CK_PaymentIntent_Status", "Status IN (N'Pending', N'Processing', N'Succeeded', N'Failed', N'Unknown', N'Expired', N'Cancelled')");
                    table.CheckConstraint("CK_PaymentIntent_Target", "(PaymentType = N'InitialConsultation' AND BookingAttemptId IS NOT NULL AND EncounterId IS NULL)\nOR (PaymentType = N'CLSOrderBatch' AND EncounterId IS NOT NULL AND BookingAttemptId IS NULL)");
                    table.CheckConstraint("CK_PaymentIntent_TotalExpectedAmount", "TotalExpectedAmount > 0");
                    table.ForeignKey(
                        name: "FK_PaymentIntent_AppUser",
                        column: x => x.PayerAppUserId,
                        principalSchema: "dbo",
                        principalTable: "AppUser",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentIntent_BookingAttempt",
                        column: x => x.BookingAttemptId,
                        principalSchema: "dbo",
                        principalTable: "BookingAttempt",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentIntent_Encounter",
                        column: x => x.EncounterId,
                        principalSchema: "dbo",
                        principalTable: "Encounter",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentIntent_Patient",
                        column: x => x.PatientId,
                        principalSchema: "dbo",
                        principalTable: "Patient",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Prescription",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Prescription_Id"),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneralInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    PrescribedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Draft'")
                        .Annotation("Relational:DefaultConstraintName", "DF_Prescription_Status")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescription", x => x.Id);
                    table.UniqueConstraint("UQ_Prescription_PrescriptionCode", x => x.PrescriptionCode);
                    table.CheckConstraint("CK_Prescription_Status", "Status IN (N'Draft', N'Issued', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_Prescription_Doctor",
                        column: x => x.PrescribedByDoctorId,
                        principalSchema: "dbo",
                        principalTable: "Doctor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Prescription_Encounter",
                        column: x => x.EncounterId,
                        principalSchema: "dbo",
                        principalTable: "Encounter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrder",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ServiceOrder_Id"),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HasActiveReturnTicket = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_ServiceOrder_HasActiveReturnTicket"),
                    OrderBatchStatus = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Draft'")
                        .Annotation("Relational:DefaultConstraintName", "DF_ServiceOrder_OrderBatchStatus"),
                    OrderRoundNumber = table.Column<int>(type: "int", nullable: false),
                    OrderedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PrescribedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOrder", x => x.Id);
                    table.UniqueConstraint("UQ_ServiceOrder_EncounterRound", x => new { x.EncounterId, x.OrderRoundNumber });
                    table.CheckConstraint("CK_ServiceOrder_OrderBatchStatus", "OrderBatchStatus IN (N'Draft', N'Confirmed', N'Completed', N'Cancelled')");
                    table.CheckConstraint("CK_ServiceOrder_OrderRoundNumber", "OrderRoundNumber >= 1");
                    table.ForeignKey(
                        name: "FK_ServiceOrder_Doctor",
                        column: x => x.PrescribedByDoctorId,
                        principalSchema: "dbo",
                        principalTable: "Doctor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceOrder_Encounter",
                        column: x => x.EncounterId,
                        principalSchema: "dbo",
                        principalTable: "Encounter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VitalSign",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_VitalSign_Id"),
                    BodyTemperature = table.Column<decimal>(type: "decimal(4,1)", nullable: true),
                    DiastolicBP = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeightCm = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    PulseRate = table.Column<int>(type: "int", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RecordedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpO2 = table.Column<decimal>(type: "decimal(4,1)", nullable: true),
                    SystolicBP = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(5,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalSign", x => x.Id);
                    table.CheckConstraint("CK_VitalSign_BodyTemperature", "BodyTemperature > 0");
                    table.CheckConstraint("CK_VitalSign_DiastolicBP", "DiastolicBP > 0");
                    table.CheckConstraint("CK_VitalSign_HeightCm", "HeightCm > 0");
                    table.CheckConstraint("CK_VitalSign_PulseRate", "PulseRate > 0");
                    table.CheckConstraint("CK_VitalSign_SpO2", "SpO2 BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_VitalSign_SystolicBP", "SystolicBP > 0");
                    table.CheckConstraint("CK_VitalSign_WeightKg", "WeightKg > 0");
                    table.ForeignKey(
                        name: "FK_VitalSign_Encounter",
                        column: x => x.EncounterId,
                        principalSchema: "dbo",
                        principalTable: "Encounter",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VitalSign_Staff",
                        column: x => x.RecordedByStaffId,
                        principalSchema: "dbo",
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransaction",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentTransaction_Id"),
                    Currency = table.Column<string>(type: "char(3)", nullable: false, defaultValueSql: "'VND'")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentTransaction_Currency"),
                    PaymentIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Pending'")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentTransaction_PaymentStatus"),
                    ProviderRawData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "varchar(100)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionReference = table.Column<string>(type: "varchar(64)", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransaction", x => x.Id);
                    table.UniqueConstraint("UQ_PaymentTransaction_TransactionReference", x => x.TransactionReference);
                    table.CheckConstraint("CK_PaymentTransaction_Currency", "Currency = 'VND'");
                    table.CheckConstraint("CK_PaymentTransaction_PaymentStatus", "PaymentStatus IN (N'Pending', N'Success', N'Failed', N'Unknown')");
                    table.CheckConstraint("CK_PaymentTransaction_TransactionAmount", "TransactionAmount > 0");
                    table.ForeignKey(
                        name: "FK_PaymentTransaction_PaymentIntent",
                        column: x => x.PaymentIntentId,
                        principalSchema: "dbo",
                        principalTable: "PaymentIntent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItem",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PrescriptionItem_Id"),
                    ActiveIngredientSnapshot = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    MedicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicationNameSnapshot = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Route = table.Column<string>(type: "nvarchar(15)", nullable: false),
                    UsageInstructions = table.Column<string>(type: "nvarchar(1000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItem", x => x.Id);
                    table.CheckConstraint("CK_PrescriptionItem_DurationDays", "DurationDays > 0");
                    table.CheckConstraint("CK_PrescriptionItem_Quantity", "Quantity > 0");
                    table.CheckConstraint("CK_PrescriptionItem_Route", "Route IN (N'Oral', N'Topical', N'Injection', N'Inhalation')");
                    table.ForeignKey(
                        name: "FK_PrescriptionItem_MedicationCatalog",
                        column: x => x.MedicationId,
                        principalSchema: "dbo",
                        principalTable: "MedicationCatalog",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrescriptionItem_Prescription",
                        column: x => x.PrescriptionId,
                        principalSchema: "dbo",
                        principalTable: "Prescription",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrderItem",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ServiceOrderItem_Id"),
                    AppliedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_ServiceOrderItem_IsMandatory"),
                    ItemStatus = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Ordered'")
                        .Annotation("Relational:DefaultConstraintName", "DF_ServiceOrderItem_ItemStatus"),
                    MedicalServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceNameSnapshot = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOrderItem", x => x.Id);
                    table.CheckConstraint("CK_ServiceOrderItem_AppliedPrice", "AppliedPrice >= 0");
                    table.CheckConstraint("CK_ServiceOrderItem_ItemStatus", "ItemStatus IN (N'Ordered', N'SampleCollected', N'InProgress', N'ServiceFinished', N'Completed', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ServiceOrderItem_MedicalService",
                        column: x => x.MedicalServiceId,
                        principalSchema: "dbo",
                        principalTable: "MedicalService",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceOrderItem_ServiceOrder",
                        column: x => x.ServiceOrderId,
                        principalSchema: "dbo",
                        principalTable: "ServiceOrder",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvent",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentEvent_Id"),
                    EventId = table.Column<string>(type: "varchar(100)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    PayloadHash = table.Column<string>(type: "char(64)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentEvent_ReceivedAtUtc"),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false, defaultValueSql: "N'Received'")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentEvent_Status")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvent", x => x.Id);
                    table.UniqueConstraint("UK_ProviderEvent", x => new { x.Provider, x.EventId });
                    table.CheckConstraint("CK_PaymentEvent_EventType", "EventType IN (N'PaymentSuccess', N'PaymentRefund')");
                    table.CheckConstraint("CK_PaymentEvent_PayloadJson", "ISJSON(PayloadJson) = 1");
                    table.CheckConstraint("CK_PaymentEvent_Provider", "Provider IN (N'VNPT', N'MoMo')");
                    table.CheckConstraint("CK_PaymentEvent_Status", "Status IN (N'Received', N'Processed', N'DuplicateIgnored', N'Error')");
                    table.ForeignKey(
                        name: "FK_PaymentEvent_PaymentTransaction",
                        column: x => x.PaymentTransactionId,
                        principalSchema: "dbo",
                        principalTable: "PaymentTransaction",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BillingItem",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_BillingItem_Id"),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_BillingItem_CreatedAtUtc"),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemNameSnapshot = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_BillingItem_PaidAmount"),
                    PriceListItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_BillingItem_Quantity"),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValueSql: "0")
                        .Annotation("Relational:DefaultConstraintName", "DF_BillingItem_RefundedAmount"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ServiceOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(15)", nullable: false, defaultValueSql: "N'Pending'")
                        .Annotation("Relational:DefaultConstraintName", "DF_BillingItem_Status"),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, computedColumnSql: "CAST(UnitPrice * Quantity AS decimal(18,2))", stored: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingItem", x => x.Id);
                    table.CheckConstraint("CK_BillingItem_Amounts", "PaidAmount >= 0 AND RefundedAmount >= 0 AND RefundedAmount <= PaidAmount AND PaidAmount <= UnitPrice * Quantity");
                    table.CheckConstraint("CK_BillingItem_ItemType", "ItemType IN (N'ConsultationFee', N'CLSService')");
                    table.CheckConstraint("CK_BillingItem_Quantity", "Quantity > 0");
                    table.CheckConstraint("CK_BillingItem_Status", "Status IN (N'Pending', N'PartiallyPaid', N'Paid', N'Cancelled', N'Waived')");
                    table.CheckConstraint("CK_BillingItem_Target", "(ItemType = N'ConsultationFee' AND AppointmentId IS NOT NULL AND ServiceOrderItemId IS NULL)\nOR (ItemType = N'CLSService' AND ServiceOrderItemId IS NOT NULL AND AppointmentId IS NULL)");
                    table.CheckConstraint("CK_BillingItem_UnitPrice", "UnitPrice >= 0");
                    table.ForeignKey(
                        name: "FK_BillingItem_Appointment",
                        column: x => x.AppointmentId,
                        principalSchema: "dbo",
                        principalTable: "Appointment",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillingItem_Encounter",
                        column: x => x.EncounterId,
                        principalSchema: "dbo",
                        principalTable: "Encounter",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillingItem_PriceListItem",
                        column: x => x.PriceListItemId,
                        principalSchema: "dbo",
                        principalTable: "PriceListItem",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillingItem_ServiceOrderItem",
                        column: x => x.ServiceOrderItemId,
                        principalSchema: "dbo",
                        principalTable: "ServiceOrderItem",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CLSResult",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_CLSResult_Id"),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ApprovedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DetailedFindings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PerformedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ResultStatus = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Draft'")
                        .Annotation("Relational:DefaultConstraintName", "DF_CLSResult_ResultStatus"),
                    ResultSummary = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ServiceOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLSResult", x => x.Id);
                    table.UniqueConstraint("UQ_CLSResult_ServiceOrderItemId", x => x.ServiceOrderItemId);
                    table.CheckConstraint("CK_CLSResult_ResultStatus", "ResultStatus IN (N'Draft', N'Approved', N'Published', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_CLSResult_ApprovedBy",
                        column: x => x.ApprovedByStaffId,
                        principalSchema: "dbo",
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CLSResult_PerformedBy",
                        column: x => x.PerformedByStaffId,
                        principalSchema: "dbo",
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CLSResult_ServiceOrderItem",
                        column: x => x.ServiceOrderItemId,
                        principalSchema: "dbo",
                        principalTable: "ServiceOrderItem",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QueueTicket",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_QueueTicket_Id"),
                    CalledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    QueueStatus = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Waiting'")
                        .Annotation("Relational:DefaultConstraintName", "DF_QueueTicket_QueueStatus"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    ServedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ServiceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TicketNumber = table.Column<string>(type: "varchar(20)", nullable: false),
                    TicketType = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueTicket", x => x.Id);
                    table.CheckConstraint("CK_QueueTicket_Link", "(TicketType = N'InitialConsultation' AND ServiceOrderId IS NULL AND ServiceOrderItemId IS NULL)\nOR (TicketType = N'CLS' AND ServiceOrderItemId IS NOT NULL AND ServiceOrderId IS NULL)\nOR (TicketType = N'ReturnConsultation' AND ServiceOrderId IS NOT NULL AND ServiceOrderItemId IS NULL)");
                    table.CheckConstraint("CK_QueueTicket_QueueStatus", "QueueStatus IN (N'Waiting', N'Called', N'Deferred', N'InProgress', N'Finished', N'Cancelled')");
                    table.CheckConstraint("CK_QueueTicket_SequenceNumber", "SequenceNumber >= 1");
                    table.CheckConstraint("CK_QueueTicket_TicketType", "TicketType IN (N'InitialConsultation', N'CLS', N'ReturnConsultation')");
                    table.ForeignKey(
                        name: "FK_QueueTicket_Encounter",
                        column: x => x.EncounterId,
                        principalSchema: "dbo",
                        principalTable: "Encounter",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QueueTicket_Room",
                        column: x => x.TargetRoomId,
                        principalSchema: "dbo",
                        principalTable: "Room",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QueueTicket_ServiceOrder",
                        column: x => x.ServiceOrderId,
                        principalSchema: "dbo",
                        principalTable: "ServiceOrder",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QueueTicket_ServiceOrderItem",
                        column: x => x.ServiceOrderItemId,
                        principalSchema: "dbo",
                        principalTable: "ServiceOrderItem",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QueueTicket_ServiceStation",
                        column: x => x.TargetStationId,
                        principalSchema: "dbo",
                        principalTable: "ServiceStation",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocation",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PaymentAllocation_Id"),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    BillingItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocation", x => x.Id);
                    table.UniqueConstraint("UQ_PaymentAllocation_TransactionItem", x => new { x.PaymentTransactionId, x.BillingItemId });
                    table.CheckConstraint("CK_PaymentAllocation_AllocatedAmount", "AllocatedAmount > 0");
                    table.ForeignKey(
                        name: "FK_PaymentAllocation_BillingItem",
                        column: x => x.BillingItemId,
                        principalSchema: "dbo",
                        principalTable: "BillingItem",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentAllocation_PaymentTransaction",
                        column: x => x.PaymentTransactionId,
                        principalSchema: "dbo",
                        principalTable: "PaymentTransaction",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RefundRequest",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RefundRequest_Id"),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillingItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DecisionByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalPaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyRuleApplied = table.Column<string>(type: "nvarchar(30)", nullable: false),
                    RefundReason = table.Column<string>(type: "nvarchar(30)", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RefundRequest_RequestedAtUtc"),
                    Status = table.Column<string>(type: "nvarchar(10)", nullable: false, defaultValueSql: "N'Pending'")
                        .Annotation("Relational:DefaultConstraintName", "DF_RefundRequest_Status")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundRequest", x => x.Id);
                    table.CheckConstraint("CK_RefundRequest_PolicyRuleApplied", "PolicyRuleApplied IN (N'FullRefundPre0500', N'CustomApproval')");
                    table.CheckConstraint("CK_RefundRequest_RefundReason", "RefundReason IN (N'CancelledBefore0500', N'SystemError', N'DuplicatePayment', N'HospitalCancelled')");
                    table.CheckConstraint("CK_RefundRequest_RequestedAmount", "RequestedAmount > 0");
                    table.CheckConstraint("CK_RefundRequest_Status", "Status IN (N'Pending', N'Approved', N'Rejected', N'Settled', N'Failed')");
                    table.ForeignKey(
                        name: "FK_RefundRequest_Appointment",
                        column: x => x.AppointmentId,
                        principalSchema: "dbo",
                        principalTable: "Appointment",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RefundRequest_BillingItem",
                        column: x => x.BillingItemId,
                        principalSchema: "dbo",
                        principalTable: "BillingItem",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RefundRequest_PaymentTransaction",
                        column: x => x.OriginalPaymentTransactionId,
                        principalSchema: "dbo",
                        principalTable: "PaymentTransaction",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RefundRequest_Staff",
                        column: x => x.DecisionByStaffId,
                        principalSchema: "dbo",
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResultAttachment",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ResultAttachment_Id"),
                    AttachmentVersion = table.Column<int>(type: "int", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_ResultAttachment_AttachmentVersion"),
                    CLSResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileContentType = table.Column<string>(type: "varchar(100)", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false, defaultValueSql: "1")
                        .Annotation("Relational:DefaultConstraintName", "DF_ResultAttachment_IsDraft"),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", nullable: false),
                    R2StorageKey = table.Column<string>(type: "varchar(500)", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultAttachment", x => x.Id);
                    table.UniqueConstraint("UQ_ResultAttachment_R2StorageKey", x => x.R2StorageKey);
                    table.CheckConstraint("CK_ResultAttachment_AttachmentVersion", "AttachmentVersion >= 1");
                    table.CheckConstraint("CK_ResultAttachment_FileSizeBytes", "FileSizeBytes > 0");
                    table.ForeignKey(
                        name: "FK_ResultAttachment_CLSResult",
                        column: x => x.CLSResultId,
                        principalSchema: "dbo",
                        principalTable: "CLSResult",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ActivePatientEngagement",
                schema: "dbo",
                columns: table => new
                {
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquiredAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CurrentStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivePatientEngagement", x => x.PatientId);
                    table.UniqueConstraint("UQ_ActivePatientEngagement_CurrentStationId", x => x.CurrentStationId);
                    table.UniqueConstraint("UQ_ActivePatientEngagement_CurrentTicketId", x => x.CurrentTicketId);
                    table.ForeignKey(
                        name: "FK_ActivePatientEngagement_Encounter",
                        column: x => x.EncounterId,
                        principalSchema: "dbo",
                        principalTable: "Encounter",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActivePatientEngagement_Patient",
                        column: x => x.PatientId,
                        principalSchema: "dbo",
                        principalTable: "Patient",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActivePatientEngagement_QueueTicket",
                        column: x => x.CurrentTicketId,
                        principalSchema: "dbo",
                        principalTable: "QueueTicket",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActivePatientEngagement_ServiceStation",
                        column: x => x.CurrentStationId,
                        principalSchema: "dbo",
                        principalTable: "ServiceStation",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ActiveStationAllocation",
                schema: "dbo",
                columns: table => new
                {
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CurrentPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveStationAllocation", x => x.StationId);
                    table.UniqueConstraint("UQ_ActiveStationAllocation_CurrentPatientId", x => x.CurrentPatientId);
                    table.UniqueConstraint("UQ_ActiveStationAllocation_CurrentTicketId", x => x.CurrentTicketId);
                    table.ForeignKey(
                        name: "FK_ActiveStationAllocation_Patient",
                        column: x => x.CurrentPatientId,
                        principalSchema: "dbo",
                        principalTable: "Patient",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActiveStationAllocation_QueueTicket",
                        column: x => x.CurrentTicketId,
                        principalSchema: "dbo",
                        principalTable: "QueueTicket",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActiveStationAllocation_ServiceStation",
                        column: x => x.StationId,
                        principalSchema: "dbo",
                        principalTable: "ServiceStation",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RefundTransaction",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RefundTransaction_Id"),
                    Currency = table.Column<string>(type: "char(3)", nullable: false, defaultValueSql: "'VND'")
                        .Annotation("Relational:DefaultConstraintName", "DF_RefundTransaction_Currency"),
                    OriginalPaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ProviderRefundTransactionId = table.Column<string>(type: "varchar(100)", nullable: true),
                    ProviderResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefundRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundTransaction", x => x.Id);
                    table.UniqueConstraint("UQ_RefundTransaction_RefundRequestId", x => x.RefundRequestId);
                    table.CheckConstraint("CK_RefundTransaction_Currency", "Currency = 'VND'");
                    table.CheckConstraint("CK_RefundTransaction_RefundedAmount", "RefundedAmount > 0");
                    table.CheckConstraint("CK_RefundTransaction_Status", "Status IN (N'Success', N'Failed')");
                    table.ForeignKey(
                        name: "FK_RefundTransaction_PaymentTransaction",
                        column: x => x.OriginalPaymentTransactionId,
                        principalSchema: "dbo",
                        principalTable: "PaymentTransaction",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RefundTransaction_RefundRequest",
                        column: x => x.RefundRequestId,
                        principalSchema: "dbo",
                        principalTable: "RefundRequest",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_AppointmentId",
                schema: "dbo",
                table: "ActiveBookingClaim",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_PatientId",
                schema: "dbo",
                table: "ActiveBookingClaim",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_ReservationId",
                schema: "dbo",
                table: "ActiveBookingClaim",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveBookingClaim_SessionQuotaId",
                schema: "dbo",
                table: "ActiveBookingClaim",
                column: "SessionQuotaId");

            migrationBuilder.CreateIndex(
                name: "UX_ActiveBookingClaim_DoctorDatePatient",
                schema: "dbo",
                table: "ActiveBookingClaim",
                columns: new[] { "DoctorId", "ServiceDate", "PatientId" },
                unique: true,
                filter: "IsActive = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ActivePatientEngagement_EncounterId",
                schema: "dbo",
                table: "ActivePatientEngagement",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiIdempotencyRecord_OperationId",
                schema: "dbo",
                table: "ApiIdempotencyRecord",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_PatientId",
                schema: "dbo",
                table: "Appointment",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_RescheduledFromAppointmentId",
                schema: "dbo",
                table: "Appointment",
                column: "RescheduledFromAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_SessionQuotaId",
                schema: "dbo",
                table: "Appointment",
                column: "SessionQuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_OperationId",
                schema: "dbo",
                table: "AuditLog",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_PerformedByUserId",
                schema: "dbo",
                table: "AuditLog",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Record",
                schema: "dbo",
                table: "AuditLog",
                columns: new[] { "TableName", "RecordId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_RoomAccountId",
                schema: "dbo",
                table: "AuditLog",
                column: "RoomAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_RoomDeviceSessionId",
                schema: "dbo",
                table: "AuditLog",
                column: "RoomDeviceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_StaffShiftAssignmentId",
                schema: "dbo",
                table: "AuditLog",
                column: "StaffShiftAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingItem_EncounterId",
                schema: "dbo",
                table: "BillingItem",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingItem_PriceListItemId",
                schema: "dbo",
                table: "BillingItem",
                column: "PriceListItemId");

            migrationBuilder.CreateIndex(
                name: "UX_BillingItem_AppointmentId",
                schema: "dbo",
                table: "BillingItem",
                column: "AppointmentId",
                unique: true,
                filter: "AppointmentId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_BillingItem_ServiceOrderItemId",
                schema: "dbo",
                table: "BillingItem",
                column: "ServiceOrderItemId",
                unique: true,
                filter: "ServiceOrderItemId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAttempt_AppUserId",
                schema: "dbo",
                table: "BookingAttempt",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAttempt_PatientId",
                schema: "dbo",
                table: "BookingAttempt",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAttempt_SelectedDoctorId",
                schema: "dbo",
                table: "BookingAttempt",
                column: "SelectedDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAttempt_SelectedDoctorRankId",
                schema: "dbo",
                table: "BookingAttempt",
                column: "SelectedDoctorRankId");

            migrationBuilder.CreateIndex(
                name: "IX_CLSResult_ApprovedByStaffId",
                schema: "dbo",
                table: "CLSResult",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CLSResult_PerformedByStaffId",
                schema: "dbo",
                table: "CLSResult",
                column: "PerformedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctor_DoctorRankId",
                schema: "dbo",
                table: "Doctor",
                column: "DoctorRankId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctor_SpecialtyId",
                schema: "dbo",
                table: "Doctor",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSessionSlotQuota_AssignedRoomId",
                schema: "dbo",
                table: "DoctorSessionSlotQuota",
                column: "AssignedRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounter_OriginalDoctorId",
                schema: "dbo",
                table: "Encounter",
                column: "OriginalDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounter_OriginalRoomId",
                schema: "dbo",
                table: "Encounter",
                column: "OriginalRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounter_PatientId",
                schema: "dbo",
                table: "Encounter",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterClinicalRecord_UpdatedByDoctorId",
                schema: "dbo",
                table: "EncounterClinicalRecord",
                column: "UpdatedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterDiagnosis_EncounterId",
                schema: "dbo",
                table: "EncounterDiagnosis",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalService_DefaultRoomId",
                schema: "dbo",
                table: "MedicalService",
                column: "DefaultRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalServiceRoom_RoomId",
                schema: "dbo",
                table: "MedicalServiceRoom",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "UX_MedicalServiceRoom_DefaultPerService",
                schema: "dbo",
                table: "MedicalServiceRoom",
                column: "MedicalServiceId",
                unique: true,
                filter: "IsDefaultRoom = 1 AND IsActive = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Operation_BookingAttemptId",
                schema: "dbo",
                table: "Operation",
                column: "BookingAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_Operation_InitiatedByUserId",
                schema: "dbo",
                table: "Operation",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OperationId",
                schema: "dbo",
                table: "OutboxMessage",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Pending",
                schema: "dbo",
                table: "OutboxMessage",
                column: "CreatedAtUtc",
                filter: "Status IN (N'Pending', N'Processing')");

            migrationBuilder.CreateIndex(
                name: "UX_Patient_NationalId",
                schema: "dbo",
                table: "Patient",
                column: "NationalId",
                unique: true,
                filter: "NationalId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PatientRepresentative_PatientId",
                schema: "dbo",
                table: "PatientRepresentative",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "UX_PatientRepresentative_ActiveLink",
                schema: "dbo",
                table: "PatientRepresentative",
                columns: new[] { "AppUserId", "PatientId" },
                unique: true,
                filter: "IsActive = 1");

            migrationBuilder.CreateIndex(
                name: "UX_PatientRepresentative_SelfPerAccount",
                schema: "dbo",
                table: "PatientRepresentative",
                column: "AppUserId",
                unique: true,
                filter: "IsActive = 1 AND Relationship = N'Self'");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_BillingItemId",
                schema: "dbo",
                table: "PaymentAllocation",
                column: "BillingItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvent_PayloadHash",
                schema: "dbo",
                table: "PaymentEvent",
                column: "PayloadHash");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvent_PaymentTransactionId",
                schema: "dbo",
                table: "PaymentEvent",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntent_BookingAttemptId",
                schema: "dbo",
                table: "PaymentIntent",
                column: "BookingAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntent_EncounterId",
                schema: "dbo",
                table: "PaymentIntent",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntent_PatientId",
                schema: "dbo",
                table: "PaymentIntent",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntent_PayerAppUserId",
                schema: "dbo",
                table: "PaymentIntent",
                column: "PayerAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransaction_PaymentIntentId",
                schema: "dbo",
                table: "PaymentTransaction",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentTransaction_ProviderTransactionId",
                schema: "dbo",
                table: "PaymentTransaction",
                column: "ProviderTransactionId",
                unique: true,
                filter: "ProviderTransactionId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Prescription_EncounterId",
                schema: "dbo",
                table: "Prescription",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescription_PrescribedByDoctorId",
                schema: "dbo",
                table: "Prescription",
                column: "PrescribedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItem_MedicationId",
                schema: "dbo",
                table: "PrescriptionItem",
                column: "MedicationId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItem_PrescriptionId",
                schema: "dbo",
                table: "PrescriptionItem",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_DoctorId",
                schema: "dbo",
                table: "PriceListItem",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_DoctorRankId",
                schema: "dbo",
                table: "PriceListItem",
                column: "DoctorRankId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_MedicalServiceId",
                schema: "dbo",
                table: "PriceListItem",
                column: "MedicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_PriceListId",
                schema: "dbo",
                table: "PriceListItem",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_EncounterId",
                schema: "dbo",
                table: "QueueTicket",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_ServiceOrderId",
                schema: "dbo",
                table: "QueueTicket",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_ServiceOrderItemId",
                schema: "dbo",
                table: "QueueTicket",
                column: "ServiceOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_TargetRoomId",
                schema: "dbo",
                table: "QueueTicket",
                column: "TargetRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTicket_TargetStationId",
                schema: "dbo",
                table: "QueueTicket",
                column: "TargetStationId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequest_AppointmentId",
                schema: "dbo",
                table: "RefundRequest",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequest_BillingItemId",
                schema: "dbo",
                table: "RefundRequest",
                column: "BillingItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequest_DecisionByStaffId",
                schema: "dbo",
                table: "RefundRequest",
                column: "DecisionByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequest_OriginalPaymentTransactionId",
                schema: "dbo",
                table: "RefundRequest",
                column: "OriginalPaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundTransaction_OriginalPaymentTransactionId",
                schema: "dbo",
                table: "RefundTransaction",
                column: "OriginalPaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "UX_RefundTransaction_ProviderRefundTransactionId",
                schema: "dbo",
                table: "RefundTransaction",
                column: "ProviderRefundTransactionId",
                unique: true,
                filter: "ProviderRefundTransactionId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_AppUserId",
                schema: "dbo",
                table: "Reservation",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_BookingAttemptId",
                schema: "dbo",
                table: "Reservation",
                column: "BookingAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_PatientId",
                schema: "dbo",
                table: "Reservation",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_SessionQuotaId",
                schema: "dbo",
                table: "Reservation",
                column: "SessionQuotaId");

            migrationBuilder.CreateIndex(
                name: "UX_Reservation_IdempotencyKey",
                schema: "dbo",
                table: "Reservation",
                column: "IdempotencyKey",
                unique: true,
                filter: "IdempotencyKey IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAttachment_CLSResultId",
                schema: "dbo",
                table: "ResultAttachment",
                column: "CLSResultId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomDeviceSession_RoomAccountId",
                schema: "dbo",
                table: "RoomDeviceSession",
                column: "RoomAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrder_PrescribedByDoctorId",
                schema: "dbo",
                table: "ServiceOrder",
                column: "PrescribedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderItem_MedicalServiceId",
                schema: "dbo",
                table: "ServiceOrderItem",
                column: "MedicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderItem_ServiceOrderId",
                schema: "dbo",
                table: "ServiceOrderItem",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStation_RoomId",
                schema: "dbo",
                table: "ServiceStation",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffShiftAssignment_RoomId",
                schema: "dbo",
                table: "StaffShiftAssignment",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffShiftAssignment_StaffId",
                schema: "dbo",
                table: "StaffShiftAssignment",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_VitalSign_EncounterId",
                schema: "dbo",
                table: "VitalSign",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_VitalSign_RecordedByStaffId",
                schema: "dbo",
                table: "VitalSign",
                column: "RecordedByStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveBookingClaim",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ActivePatientEngagement",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ActiveStationAllocation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ApiIdempotencyRecord",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EncounterClinicalRecord",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EncounterDiagnosis",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MedicalServiceRoom",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PatientRepresentative",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PaymentAllocation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PaymentEvent",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PrescriptionItem",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RefundTransaction",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ResultAttachment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "VitalSign",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "QueueTicket",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RoomDeviceSession",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StaffShiftAssignment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Operation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MedicationCatalog",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Prescription",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RefundRequest",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CLSResult",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ServiceStation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RoomAccount",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "BillingItem",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PaymentTransaction",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PriceListItem",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ServiceOrderItem",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PaymentIntent",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PriceList",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MedicalService",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ServiceOrder",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Encounter",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Appointment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Reservation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "BookingAttempt",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DoctorSessionSlotQuota",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Patient",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Doctor",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Room",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DoctorRank",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Specialty",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Staff",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AppUser",
                schema: "dbo");
        }
    }
}
