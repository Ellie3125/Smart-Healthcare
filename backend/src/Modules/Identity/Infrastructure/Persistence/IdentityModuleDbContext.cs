using Microsoft.EntityFrameworkCore;
using SmartHealthcare.Identity.Domain;

namespace SmartHealthcare.Identity.Infrastructure.Persistence;

/// <summary>
/// DbContext riêng của module Identity, chỉ map bảng AppUser và Staff.
/// Database-first: schema do infrastructure/sqlserver/schema/SmartHealthcare_v2.2.sql tạo; kiểu cột ở đây phải khớp script.
/// </summary>
public sealed class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options) : DbContext(options)
{
    internal DbSet<UserAccount> Accounts => Set<UserAccount>();

    internal DbSet<Staff> Staff => Set<Staff>();

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
        modelBuilder.Entity<UserAccount>(e =>
        {
            e.ToTable("AppUser", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.UserName).HasMaxLength(AccountIdentifiers.UserNameMaxLength);
            e.Property(x => x.Email).HasMaxLength(AccountIdentifiers.EmailMaxLength);
            e.Property(x => x.PhoneNumber).HasMaxLength(AccountIdentifiers.PhoneMaxLength).IsUnicode(false);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
        });

        modelBuilder.Entity<Staff>(e =>
        {
            e.ToTable("Staff", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.StaffCode).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.FullName).HasMaxLength(150);
            e.Property(x => x.StaffType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
            e.HasOne<UserAccount>().WithOne().HasForeignKey<Staff>(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
