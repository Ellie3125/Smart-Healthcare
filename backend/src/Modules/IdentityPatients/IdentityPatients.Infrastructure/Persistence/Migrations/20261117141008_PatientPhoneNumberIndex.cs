using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPatients.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientPhoneNumberIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Patient_PhoneNumber",
                schema: "identity",
                table: "Patient",
                column: "PhoneNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patient_PhoneNumber",
                schema: "identity",
                table: "Patient");
        }
    }
}
