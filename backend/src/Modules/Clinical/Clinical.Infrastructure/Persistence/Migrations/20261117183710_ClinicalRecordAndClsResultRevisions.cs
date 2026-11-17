using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalRecordAndClsResultRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CLSResultRevision",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CLSResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResultSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetailedFindings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLSResultRevision", x => x.Id);
                    table.CheckConstraint("CK_CLSResultRevision_Kind", "([Kind] = N'Published' AND [RevisionNumber] = 1) OR ([Kind] IN (N'Corrected', N'Withdrawn') AND [RevisionNumber] > 1 AND [Reason] IS NOT NULL AND LEN(LTRIM([Reason])) > 0)");
                    table.CheckConstraint("CK_CLSResultRevision_RevisionNumber_Positive", "[RevisionNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_CLSResultRevision_CLSResult_CLSResultId",
                        column: x => x.CLSResultId,
                        principalSchema: "clinical",
                        principalTable: "CLSResult",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncounterClinicalRecordRevision",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicalRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChiefComplaint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Symptoms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MedicalHistory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClinicalExaminationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitialClinicalImpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostCLSConclusion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TreatmentPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FollowUpInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiagnosesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterClinicalRecordRevision", x => x.Id);
                    table.CheckConstraint("CK_EncounterClinicalRecordRevision_DiagnosesJson", "ISJSON([DiagnosesJson]) = 1");
                    table.CheckConstraint("CK_EncounterClinicalRecordRevision_Kind", "([Kind] = N'Published' AND [RevisionNumber] = 1) OR ([Kind] = N'Amended' AND [RevisionNumber] > 1 AND [Reason] IS NOT NULL AND LEN(LTRIM([Reason])) > 0)");
                    table.CheckConstraint("CK_EncounterClinicalRecordRevision_RevisionNumber_Positive", "[RevisionNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_EncounterClinicalRecordRevision_EncounterClinicalRecord_ClinicalRecordId",
                        column: x => x.ClinicalRecordId,
                        principalSchema: "clinical",
                        principalTable: "EncounterClinicalRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CLSResultRevision_CLSResultId_RevisionNumber",
                schema: "clinical",
                table: "CLSResultRevision",
                columns: new[] { "CLSResultId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncounterClinicalRecordRevision_ClinicalRecordId_RevisionNumber",
                schema: "clinical",
                table: "EncounterClinicalRecordRevision",
                columns: new[] { "ClinicalRecordId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CLSResultRevision",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "EncounterClinicalRecordRevision",
                schema: "clinical");
        }
    }
}
