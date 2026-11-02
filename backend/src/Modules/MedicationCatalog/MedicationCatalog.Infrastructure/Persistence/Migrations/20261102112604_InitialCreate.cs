using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicationCatalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "medication");

            migrationBuilder.CreateTable(
                name: "MedicationCatalog",
                schema: "medication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicationCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationCatalog_MedicationCode",
                schema: "medication",
                table: "MedicationCatalog",
                column: "MedicationCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicationCatalog",
                schema: "medication");
        }
    }
}
