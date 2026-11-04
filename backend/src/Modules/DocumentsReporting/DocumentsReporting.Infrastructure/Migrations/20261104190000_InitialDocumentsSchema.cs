using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentsReporting.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialDocumentsSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "documents");

        migrationBuilder.CreateTable(
            name: "ResultAttachment",
            schema: "documents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CLSResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                QueueTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                R2StorageKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                FileContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256Hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UploadedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UploadStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ResultAttachment", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ResultAttachment_CLSResultId",
            schema: "documents",
            table: "ResultAttachment",
            column: "CLSResultId");

        migrationBuilder.CreateIndex(
            name: "IX_ResultAttachment_EncounterId",
            schema: "documents",
            table: "ResultAttachment",
            column: "EncounterId");

        migrationBuilder.CreateIndex(
            name: "IX_ResultAttachment_PatientId_EncounterId",
            schema: "documents",
            table: "ResultAttachment",
            columns: new[] { "PatientId", "EncounterId" });

        migrationBuilder.CreateIndex(
            name: "IX_ResultAttachment_QueueTicketId",
            schema: "documents",
            table: "ResultAttachment",
            column: "QueueTicketId");

        migrationBuilder.CreateIndex(
            name: "IX_ResultAttachment_R2StorageKey",
            schema: "documents",
            table: "ResultAttachment",
            column: "R2StorageKey",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ResultAttachment",
            schema: "documents");
    }
}
