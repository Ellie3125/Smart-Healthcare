using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthcare.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_ErdV22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "DoctorRank",
                schema: "catalog",
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
                    table.CheckConstraint("CK_DoctorRank_RankCode", "[RankCode] IN (N'SpecialistI', N'SpecialistII', N'Master', N'Doctor', N'AssociateProf', N'Professor')");
                });

            migrationBuilder.CreateTable(
                name: "MedicalService",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServiceCategory = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DefaultRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsMandatoryDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalService", x => x.Id);
                    table.CheckConstraint("CK_MedicalService_ServiceCategory", "[ServiceCategory] IN (N'Laboratory', N'Radiology', N'Ultrasound', N'Endoscopy')");
                });

            migrationBuilder.CreateTable(
                name: "MedicationCatalog",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MedicationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActiveIngredient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DosageForm = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationCatalog", x => x.Id);
                    table.CheckConstraint("CK_MedicationCatalog_DosageForm", "[DosageForm] IN (N'Tablet', N'Capsule', N'Liquid', N'Injection')");
                    table.CheckConstraint("CK_MedicationCatalog_Unit", "[Unit] IN (N'Vien', N'Goi', N'Chai', N'Ong')");
                });

            migrationBuilder.CreateTable(
                name: "PriceList",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceListCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PriceListName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceList", x => x.Id);
                    table.CheckConstraint("CK_PriceList_Currency", "[Currency] IN (N'VND')");
                });

            migrationBuilder.CreateTable(
                name: "Specialty",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SpecialtyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specialty", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalServiceRoom",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefaultRoom = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalServiceRoom", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalServiceRoom_MedicalService_MedicalServiceId",
                        column: x => x.MedicalServiceId,
                        principalSchema: "catalog",
                        principalTable: "MedicalService",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceListItem",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DoctorRankId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MedicalServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListItem", x => x.Id);
                    table.CheckConstraint("CK_PriceListItem_ItemType", "[ItemType] IN (N'ConsultationFee', N'CLSService')");
                    table.CheckConstraint("CK_PriceListItem_ItemTypeTarget", "([ItemType] = N'ConsultationFee' AND ([DoctorRankId] IS NOT NULL OR [DoctorId] IS NOT NULL) AND [MedicalServiceId] IS NULL) OR ([ItemType] = N'CLSService' AND [MedicalServiceId] IS NOT NULL AND [DoctorRankId] IS NULL AND [DoctorId] IS NULL)");
                    table.CheckConstraint("CK_PriceListItem_UnitPrice", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_PriceListItem_DoctorRank_DoctorRankId",
                        column: x => x.DoctorRankId,
                        principalSchema: "catalog",
                        principalTable: "DoctorRank",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceListItem_MedicalService_MedicalServiceId",
                        column: x => x.MedicalServiceId,
                        principalSchema: "catalog",
                        principalTable: "MedicalService",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceListItem_PriceList_PriceListId",
                        column: x => x.PriceListId,
                        principalSchema: "catalog",
                        principalTable: "PriceList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorRank_RankCode",
                schema: "catalog",
                table: "DoctorRank",
                column: "RankCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicalService_DefaultRoomId",
                schema: "catalog",
                table: "MedicalService",
                column: "DefaultRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalService_ServiceCode",
                schema: "catalog",
                table: "MedicalService",
                column: "ServiceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicalServiceRoom_MedicalServiceId",
                schema: "catalog",
                table: "MedicalServiceRoom",
                column: "MedicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalServiceRoom_RoomId",
                schema: "catalog",
                table: "MedicalServiceRoom",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationCatalog_MedicationCode",
                schema: "catalog",
                table: "MedicationCatalog",
                column: "MedicationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceList_PriceListCode",
                schema: "catalog",
                table: "PriceList",
                column: "PriceListCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_DoctorId",
                schema: "catalog",
                table: "PriceListItem",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_DoctorRankId",
                schema: "catalog",
                table: "PriceListItem",
                column: "DoctorRankId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_MedicalServiceId",
                schema: "catalog",
                table: "PriceListItem",
                column: "MedicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_PriceListId",
                schema: "catalog",
                table: "PriceListItem",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_Specialty_SpecialtyCode",
                schema: "catalog",
                table: "Specialty",
                column: "SpecialtyCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicalServiceRoom",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MedicationCatalog",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "PriceListItem",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Specialty",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "DoctorRank",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MedicalService",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "PriceList",
                schema: "catalog");
        }
    }
}
