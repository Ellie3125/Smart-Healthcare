using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Clinical.Infrastructure.Persistence;

#nullable disable

namespace Clinical.Infrastructure.Migrations;

[DbContext(typeof(ClinicalDbContext))]
[Migration("20261104190000_InitialClinicalSchema")]
partial class InitialClinicalSchema
{
    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasDefaultSchema("clinical")
            .HasAnnotation("ProductVersion", "9.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("Clinical.Domain.Entities.CLSResult", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid?>("ApprovedByStaffId")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime?>("CompletedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("DetailedFindings")
                    .HasColumnType("nvarchar(max)");

                b.Property<Guid>("PerformedByStaffId")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime?>("PublishedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("ResultStatus")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<string>("ResultSummary")
                    .HasColumnType("nvarchar(max)");

                b.Property<byte[]>("RowVersion")
                    .IsConcurrencyToken()
                    .IsRequired()
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnType("rowversion");

                b.Property<Guid>("ServiceOrderItemId")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.HasIndex("ApprovedByStaffId");

                b.HasIndex("PerformedByStaffId");

                b.HasIndex("ServiceOrderItemId")
                    .IsUnique();

                b.ToTable("CLSResult", "clinical");
            });

        modelBuilder.Entity("Clinical.Domain.Entities.Encounter", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("AppointmentId")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime>("CheckedInAtUtc")
                    .HasColumnType("datetime2");

                b.Property<DateTime?>("CompletedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("DoctorRankSnapshot")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<Guid>("OriginalDoctorId")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("OriginalRoomId")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("PatientId")
                    .HasColumnType("uniqueidentifier");

                b.Property<byte[]>("RowVersion")
                    .IsConcurrencyToken()
                    .IsRequired()
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnType("rowversion");

                b.Property<string>("SpecialtySnapshot")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.HasKey("Id");

                b.HasIndex("AppointmentId")
                    .IsUnique();

                b.HasIndex("OriginalDoctorId");

                b.HasIndex("OriginalRoomId");

                b.HasIndex("PatientId", "CheckedInAtUtc");

                b.ToTable("Encounter", "clinical");
            });

        modelBuilder.Entity("Clinical.Domain.Entities.EncounterClinicalRecord", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ChiefComplaint")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("ClinicalExaminationNotes")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("FollowUpInstructions")
                    .HasColumnType("nvarchar(max)");

                b.Property<Guid>("EncounterId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("InitialClinicalImpression")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("MedicalHistory")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("PostCLSConclusion")
                    .HasColumnType("nvarchar(max)");

                b.Property<byte[]>("RowVersion")
                    .IsConcurrencyToken()
                    .IsRequired()
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnType("rowversion");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<string>("Symptoms")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("TreatmentPlan")
                    .HasColumnType("nvarchar(max)");

                b.Property<DateTime>("UpdatedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<Guid>("UpdatedByDoctorId")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.HasIndex("EncounterId")
                    .IsUnique();

                b.HasIndex("UpdatedByDoctorId");

                b.ToTable("EncounterClinicalRecord", "clinical");
            });

        modelBuilder.Entity("Clinical.Domain.Entities.EncounterDiagnosis", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ClinicalNotes")
                    .HasColumnType("nvarchar(max)");

                b.Property<DateTime>("DiagnosedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("DiagnosisName")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("DiagnosisType")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<Guid>("EncounterId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ICD10Code")
                    .IsRequired()
                    .HasMaxLength(64)
                    .HasColumnType("nvarchar(64)");

                b.Property<bool>("IsPrimary")
                    .HasColumnType("bit");

                b.HasKey("Id");

                b.HasIndex("EncounterId");

                b.ToTable("EncounterDiagnosis", "clinical");
            });

        modelBuilder.Entity("Clinical.Domain.Entities.Prescription", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("EncounterId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("GeneralInstructions")
                    .HasColumnType("nvarchar(max)");

                b.Property<DateTime>("IssuedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<Guid>("PrescribedByDoctorId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("PrescriptionCode")
                    .IsRequired()
                    .HasMaxLength(64)
                    .HasColumnType("nvarchar(64)");

                b.Property<byte[]>("RowVersion")
                    .IsConcurrencyToken()
                    .IsRequired()
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnType("rowversion");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.HasKey("Id");

                b.HasIndex("EncounterId");

                b.HasIndex("PrescribedByDoctorId");

                b.HasIndex("PrescriptionCode")
                    .IsUnique();

                b.ToTable("Prescription", "clinical");
            });

        modelBuilder.Entity("Clinical.Domain.Entities.PrescriptionItem", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ActiveIngredientSnapshot")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Dosage")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<int>("DurationDays")
                    .HasColumnType("int");

                b.Property<string>("Frequency")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<Guid>("MedicationId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("MedicationNameSnapshot")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<Guid>("PrescriptionId")
                    .HasColumnType("uniqueidentifier");

                b.Property<int>("Quantity")
                    .HasColumnType("int");

                b.Property<string>("Route")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<string>("UsageInstructions")
                    .HasColumnType("nvarchar(max)");

                b.HasKey("Id");

                b.HasIndex("MedicationId");

                b.HasIndex("PrescriptionId");

                b.ToTable("PrescriptionItem", "clinical");
            });

        modelBuilder.Entity("Clinical.Domain.Entities.ServiceOrder", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("EncounterId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("OrderBatchStatus")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<DateTime>("OrderedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<Guid>("PrescribedByDoctorId")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.HasIndex("EncounterId");

                b.HasIndex("PrescribedByDoctorId");

                b.ToTable("ServiceOrder", "clinical");
            });

        modelBuilder.Entity("Clinical.Domain.Entities.ServiceOrderItem", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<decimal>("AppliedPrice")
                    .HasPrecision(18, 2)
                    .HasColumnType("decimal(18,2)");

                b.Property<string>("ItemStatus")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<Guid>("MedicalServiceId")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("ServiceOrderId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ServiceNameSnapshot")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.HasKey("Id");

                b.HasIndex("MedicalServiceId");

                b.HasIndex("ServiceOrderId");

                b.ToTable("ServiceOrderItem", "clinical");
            });

        modelBuilder.Entity("Clinical.Domain.Entities.VitalSign", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<decimal>("BodyTemperature")
                    .HasPrecision(4, 1)
                    .HasColumnType("decimal(4,1)");

                b.Property<decimal>("DiastolicBP")
                    .HasPrecision(6, 2)
                    .HasColumnType("decimal(6,2)");

                b.Property<Guid>("EncounterId")
                    .HasColumnType("uniqueidentifier");

                b.Property<decimal>("HeightCm")
                    .HasPrecision(5, 1)
                    .HasColumnType("decimal(5,1)");

                b.Property<int>("Pulse")
                    .HasColumnType("int");

                b.Property<DateTime>("RecordedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<Guid>("RecordedByStaffId")
                    .HasColumnType("uniqueidentifier");

                b.Property<decimal?>("SpO2")
                    .HasPrecision(5, 2)
                    .HasColumnType("decimal(5,2)");

                b.Property<decimal>("SystolicBP")
                    .HasPrecision(6, 2)
                    .HasColumnType("decimal(6,2)");

                b.Property<decimal>("WeightKg")
                    .HasPrecision(5, 2)
                    .HasColumnType("decimal(5,2)");

                b.HasKey("Id");

                b.HasIndex("EncounterId");

                b.HasIndex("RecordedByStaffId");

                b.ToTable("VitalSign", "clinical");
            });

        modelBuilder.Entity("Clinical.Domain.Entities.CLSResult", b =>
            {
                b.HasOne("Clinical.Domain.Entities.ServiceOrderItem", null)
                    .WithMany()
                    .HasForeignKey("ServiceOrderItemId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });

        modelBuilder.Entity("Clinical.Domain.Entities.EncounterClinicalRecord", b =>
            {
                b.HasOne("Clinical.Domain.Entities.Encounter", null)
                    .WithMany()
                    .HasForeignKey("EncounterId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });

        modelBuilder.Entity("Clinical.Domain.Entities.EncounterDiagnosis", b =>
            {
                b.HasOne("Clinical.Domain.Entities.Encounter", null)
                    .WithMany()
                    .HasForeignKey("EncounterId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });

        modelBuilder.Entity("Clinical.Domain.Entities.Prescription", b =>
            {
                b.HasOne("Clinical.Domain.Entities.Encounter", null)
                    .WithMany()
                    .HasForeignKey("EncounterId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });

        modelBuilder.Entity("Clinical.Domain.Entities.PrescriptionItem", b =>
            {
                b.HasOne("Clinical.Domain.Entities.Prescription", null)
                    .WithMany()
                    .HasForeignKey("PrescriptionId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });

        modelBuilder.Entity("Clinical.Domain.Entities.ServiceOrder", b =>
            {
                b.HasOne("Clinical.Domain.Entities.Encounter", null)
                    .WithMany()
                    .HasForeignKey("EncounterId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });

        modelBuilder.Entity("Clinical.Domain.Entities.ServiceOrderItem", b =>
            {
                b.HasOne("Clinical.Domain.Entities.ServiceOrder", null)
                    .WithMany()
                    .HasForeignKey("ServiceOrderId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });

        modelBuilder.Entity("Clinical.Domain.Entities.VitalSign", b =>
            {
                b.HasOne("Clinical.Domain.Entities.Encounter", null)
                    .WithMany()
                    .HasForeignKey("EncounterId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });
#pragma warning restore 612, 618
    }
}
