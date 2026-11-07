using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPatients.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncUtcDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Doctor_DefaultRoomId",
                schema: "identity",
                table: "Doctor",
                column: "DefaultRoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Doctor_DefaultRoomId",
                schema: "identity",
                table: "Doctor");
        }
    }
}
