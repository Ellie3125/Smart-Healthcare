using Microsoft.EntityFrameworkCore;
using SmartHealthcare.Patients.Domain;

namespace SmartHealthcare.Patients.Infrastructure.Persistence;

/// <summary>
/// DbContext riêng của module Patients, chỉ map bảng Patient và PatientRepresentative.
/// Database-first: schema do infrastructure/sqlserver/schema/SmartHealthcare_v2.2.sql tạo; kiểu cột ở đây phải khớp script.
/// </summary>
public sealed class PatientsModuleDbContext(DbContextOptions<PatientsModuleDbContext> options) : DbContext(options)
{
    // Tên unique index trong script schema; repository dựa vào tên này để phân biệt lỗi trùng.
    internal const string NationalIdIndex = "UX_Patient_NationalId";
    internal const string SelfLinkIndex = "UX_PatientRepresentative_SelfPerAccount";

    internal DbSet<Patient> Patients => Set<Patient>();

    internal DbSet<PatientRepresentative> Representatives => Set<PatientRepresentative>();

    public static void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseSqlServer(connectionString);

    /// <summary>SQL Server không lưu DateTimeKind; đánh dấu UTC khi đọc để JSON luôn có hậu tố Z (API-04).</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Patient>(e =>
        {
            e.ToTable("Patient", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FullName).HasMaxLength(PatientDetails.FullNameMaxLength);
            e.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.NationalId).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.PhoneNumber).HasMaxLength(15).IsUnicode(false);
            e.Property(x => x.Address).HasMaxLength(PatientDetails.AddressMaxLength);
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<PatientRepresentative>(e =>
        {
            e.ToTable("PatientRepresentative", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Relationship).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.AuthorizationProofDoc).HasMaxLength(500);
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.RevokedAtUtc).HasColumnType("datetime2(3)");
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
