using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthcare.Clinical.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_ErdV22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clinical");

            migrationBuilder.CreateTable(
                name: "Encounter",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtySnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DoctorRankSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CheckedInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounter", x => x.Id);
                    table.CheckConstraint("CK_Encounter_Status", "[Status] IN (N'InProgress', N'WaitingCLS', N'ReturningDoctor', N'Completed', N'Abandoned')");
                });

            migrationBuilder.CreateTable(
                name: "EncounterClinicalRecord",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChiefComplaint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Symptoms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MedicalHistory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClinicalExaminationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitialClinicalImpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostCLSConclusion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TreatmentPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FollowUpInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterClinicalRecord", x => x.Id);
                    table.CheckConstraint("CK_EncounterClinicalRecord_Status", "[Status] IN (N'Draft', N'Published')");
                    table.ForeignKey(
                        name: "FK_EncounterClinicalRecord_Encounter_EncounterId",
                        column: x => x.EncounterId,
                        principalSchema: "clinical",
                        principalTable: "Encounter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncounterDiagnosis",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiagnosisType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ICD10Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DiagnosisName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    ClinicalNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiagnosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterDiagnosis", x => x.Id);
                    table.CheckConstraint("CK_EncounterDiagnosis_DiagnosisType", "[DiagnosisType] IN (N'Initial', N'Final')");
                    table.ForeignKey(
                        name: "FK_EncounterDiagnosis_Encounter_EncounterId",
                        column: x => x.EncounterId,
                        principalSchema: "clinical",
                        principalTable: "Encounter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Prescription",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescribedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    GeneralInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescription", x => x.Id);
                    table.CheckConstraint("CK_Prescription_Status", "[Status] IN (N'Draft', N'Issued', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_Prescription_Encounter_EncounterId",
                        column: x => x.EncounterId,
                        principalSchema: "clinical",
                        principalTable: "Encounter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrder",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderRoundNumber = table.Column<int>(type: "int", nullable: false),
                    OrderedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrescribedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderBatchStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HasActiveReturnTicket = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOrder", x => x.Id);
                    table.CheckConstraint("CK_ServiceOrder_OrderBatchStatus", "[OrderBatchStatus] IN (N'Draft', N'Confirmed', N'Completed', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ServiceOrder_Encounter_EncounterId",
                        column: x => x.EncounterId,
                        principalSchema: "clinical",
                        principalTable: "Encounter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VitalSign",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    HeightCm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SystolicBP = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DiastolicBP = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PulseRate = table.Column<int>(type: "int", nullable: false),
                    BodyTemperature = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SpO2 = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RecordedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalSign", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VitalSign_Encounter_EncounterId",
                        column: x => x.EncounterId,
                        principalSchema: "clinical",
                        principalTable: "Encounter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItem",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicationNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActiveIngredientSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UsageInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItem", x => x.Id);
                    table.CheckConstraint("CK_PrescriptionItem_Route", "[Route] IN (N'Oral', N'Topical', N'Injection', N'Inhalation')");
                    table.ForeignKey(
                        name: "FK_PrescriptionItem_Prescription_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalSchema: "clinical",
                        principalTable: "Prescription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrderItem",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AppliedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ItemStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOrderItem", x => x.Id);
                    table.CheckConstraint("CK_ServiceOrderItem_ItemStatus", "[ItemStatus] IN (N'Ordered', N'SampleCollected', N'InProgress', N'ServiceFinished', N'Completed', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ServiceOrderItem_ServiceOrder_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalSchema: "clinical",
                        principalTable: "ServiceOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CLSResult",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DetailedFindings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PerformedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLSResult", x => x.Id);
                    table.CheckConstraint("CK_CLSResult_ResultStatus", "[ResultStatus] IN (N'Draft', N'Approved', N'Published', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_CLSResult_ServiceOrderItem_ServiceOrderItemId",
                        column: x => x.ServiceOrderItemId,
                        principalSchema: "clinical",
                        principalTable: "ServiceOrderItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultAttachment",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CLSResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    R2StorageKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileContentType = table.Column<string>(type: "nvarchar(127)", maxLength: 127, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AttachmentVersion = table.Column<int>(type: "int", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultAttachment_CLSResult_CLSResultId",
                        column: x => x.CLSResultId,
                        principalSchema: "clinical",
                        principalTable: "CLSResult",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CLSResult_ApprovedByStaffId",
                schema: "clinical",
                table: "CLSResult",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CLSResult_PerformedByStaffId",
                schema: "clinical",
                table: "CLSResult",
                column: "PerformedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CLSResult_ServiceOrderItemId",
                schema: "clinical",
                table: "CLSResult",
                column: "ServiceOrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Encounter_AppointmentId",
                schema: "clinical",
                table: "Encounter",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Encounter_OriginalDoctorId",
                schema: "clinical",
                table: "Encounter",
                column: "OriginalDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounter_OriginalRoomId",
                schema: "clinical",
                table: "Encounter",
                column: "OriginalRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounter_PatientId",
                schema: "clinical",
                table: "Encounter",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterClinicalRecord_EncounterId",
                schema: "clinical",
                table: "EncounterClinicalRecord",
                column: "EncounterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncounterClinicalRecord_UpdatedByDoctorId",
                schema: "clinical",
                table: "EncounterClinicalRecord",
                column: "UpdatedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterDiagnosis_EncounterId",
                schema: "clinical",
                table: "EncounterDiagnosis",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescription_EncounterId",
                schema: "clinical",
                table: "Prescription",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescription_PrescribedByDoctorId",
                schema: "clinical",
                table: "Prescription",
                column: "PrescribedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescription_PrescriptionCode",
                schema: "clinical",
                table: "Prescription",
                column: "PrescriptionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItem_MedicationId",
                schema: "clinical",
                table: "PrescriptionItem",
                column: "MedicationId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItem_PrescriptionId",
                schema: "clinical",
                table: "PrescriptionItem",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAttachment_CLSResultId",
                schema: "clinical",
                table: "ResultAttachment",
                column: "CLSResultId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAttachment_R2StorageKey",
                schema: "clinical",
                table: "ResultAttachment",
                column: "R2StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrder_EncounterId",
                schema: "clinical",
                table: "ServiceOrder",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrder_PrescribedByDoctorId",
                schema: "clinical",
                table: "ServiceOrder",
                column: "PrescribedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderItem_MedicalServiceId",
                schema: "clinical",
                table: "ServiceOrderItem",
                column: "MedicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderItem_ServiceOrderId",
                schema: "clinical",
                table: "ServiceOrderItem",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VitalSign_EncounterId",
                schema: "clinical",
                table: "VitalSign",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_VitalSign_RecordedByStaffId",
                schema: "clinical",
                table: "VitalSign",
                column: "RecordedByStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterClinicalRecord",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "EncounterDiagnosis",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "PrescriptionItem",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "ResultAttachment",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "VitalSign",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "Prescription",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "CLSResult",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "ServiceOrderItem",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "ServiceOrder",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "Encounter",
                schema: "clinical");
        }
    }
}
