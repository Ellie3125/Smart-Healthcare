using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Notifications.Infrastructure.Persistence;

#nullable disable

namespace Notifications.Infrastructure.Migrations;

[DbContext(typeof(WorkflowDbContext))]
partial class WorkflowDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasDefaultSchema("workflow")
            .HasAnnotation("ProductVersion", "9.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("Notifications.Domain.Entities.Operation", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid?>("BookingAttemptId")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime?>("CompletedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("FailureReason")
                    .HasColumnType("nvarchar(max)");

                b.Property<Guid>("InitiatedByUserId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("OperationType")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<byte[]>("RowVersion")
                    .IsConcurrencyToken()
                    .IsRequired()
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnType("rowversion");

                b.Property<DateTime>("StartedAtUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.Property<Guid?>("TargetEntityId")
                    .HasColumnType("uniqueidentifier");

                b.Property<string>("TargetEntityType")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("nvarchar(32)");

                b.HasKey("Id");

                b.HasIndex("BookingAttemptId");

                b.HasIndex("InitiatedByUserId");

                b.ToTable("Operation", "workflow");
            });
#pragma warning restore 612, 618
    }
}
