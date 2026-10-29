using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartHealthcare.Database.Schema;

namespace SmartHealthcare.Database;

/// <summary>
/// Mô hình đủ 46 bảng của ERD v2.2, chỉ dùng để sinh và áp migration; không dùng để đọc ghi dữ liệu.
/// Các module đọc ghi qua DbContext riêng của mình, map cùng bảng; test tích hợp kiểm hai bên khớp từng cột.
/// </summary>
public sealed class SchemaDbContext(DbContextOptions<SchemaDbContext> options) : DbContext(options)
{
    public const string Collation = "Vietnamese_100_CI_AS";

    public static void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseSqlServer(connectionString)
            // Cột bit có DEFAULT chỉ ảnh hưởng khi EF chèn dữ liệu; context này không chèn dữ liệu.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.BoolWithDefaultWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation(Collation);
        IdentityAndStaffTables.Configure(modelBuilder);
        PatientTables.Configure(modelBuilder);
        CatalogTables.Configure(modelBuilder);
        BookingTables.Configure(modelBuilder);
        ClinicalTables.Configure(modelBuilder);
        QueueTables.Configure(modelBuilder);
        BillingTables.Configure(modelBuilder);
        OperationTables.Configure(modelBuilder);
    }
}

/// <summary>
/// Cho lệnh dotnet ef. Chuỗi kết nối lấy từ tham số --connection hoặc biến SMARTHEALTHCARE_MIGRATIONS_CONNECTION;
/// lệnh migrations add/script không cần kết nối thật.
/// </summary>
public sealed class SchemaDbContextFactory : IDesignTimeDbContextFactory<SchemaDbContext>
{
    public SchemaDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SchemaDbContext>();
        SchemaDbContext.Configure(builder,
            Environment.GetEnvironmentVariable("SMARTHEALTHCARE_MIGRATIONS_CONNECTION") ?? "Server=design-time-only");
        return new SchemaDbContext(builder.Options);
    }
}
