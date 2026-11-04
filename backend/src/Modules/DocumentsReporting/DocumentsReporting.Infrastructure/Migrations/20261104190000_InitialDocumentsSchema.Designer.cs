using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DocumentsReporting.Infrastructure.Persistence;

#nullable disable

namespace DocumentsReporting.Infrastructure.Migrations;

[DbContext(typeof(DocumentsDbContext))]
[Migration("20261104190000_InitialDocumentsSchema")]
partial class InitialDocumentsSchema
{
    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasDefaultSchema("documents")
            .HasAnnotation("ProductVersion", "9.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("DocumentsReporting.Domain.Entities.ResultAttachment", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("CLSResultId")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("EncounterId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("FileContentType")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<long>("FileSizeBytes")
                    .HasColumnType("bigint");

                b.Property<string>("OriginalFileName")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<Guid>("PatientId")
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("QueueTicketId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("R2StorageKey")
                    .IsRequired()
                    .HasMaxLength(512)
                    .HasColumnType("nvarchar(512)");

                b.Property<string>("Sha256Hash")
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("nvarchar(128)");

                b.Property<string>("UploadStatus")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<DateTime>("UploadedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<Guid>("UploadedByStaffId")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.HasIndex("CLSResultId");

                b.HasIndex("EncounterId");

                b.HasIndex("QueueTicketId");

                b.HasIndex("R2StorageKey")
                    .IsUnique();

                b.HasIndex("PatientId", "EncounterId");

                b.ToTable("ResultAttachment", "documents");
            });
#pragma warning restore 612, 618
    }
}
