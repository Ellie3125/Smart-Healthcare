using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MedicationCatalog.Infrastructure.Persistence;

#nullable disable

namespace MedicationCatalog.Infrastructure.Migrations;

[DbContext(typeof(MedicationDbContext))]
partial class MedicationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasDefaultSchema("medication")
            .HasAnnotation("ProductVersion", "9.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("MedicationCatalog.Domain.Entities.MedicationCatalog", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("ActiveIngredient")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("CountryOfOrigin")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("DosageForm")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<bool>("IsActive")
                    .HasColumnType("bit");

                b.Property<string>("Manufacturer")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("MedicationCode")
                    .IsRequired()
                    .HasMaxLength(64)
                    .HasColumnType("nvarchar(64)");

                b.Property<string>("MedicationName")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("PackagingSpecification")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Strength")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<string>("Unit")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.HasKey("Id");

                b.HasIndex("MedicationCode")
                    .IsUnique();

                b.ToTable("MedicationCatalog", "medication");
            });
#pragma warning restore 612, 618
    }
}
