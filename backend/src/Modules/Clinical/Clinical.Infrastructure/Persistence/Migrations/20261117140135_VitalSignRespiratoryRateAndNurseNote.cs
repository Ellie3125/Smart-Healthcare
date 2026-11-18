using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VitalSignRespiratoryRateAndNurseNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NurseNote",
                schema: "clinical",
                table: "VitalSign",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RespiratoryRate",
                schema: "clinical",
                table: "VitalSign",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NurseNote",
                schema: "clinical",
                table: "VitalSign");

            migrationBuilder.DropColumn(
                name: "RespiratoryRate",
                schema: "clinical",
                table: "VitalSign");
        }
    }
}
