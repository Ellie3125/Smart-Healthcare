using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SmartHealthcare.Shared.Persistence;

public static class ModelBuilderExtensions
{
    /// <summary>CHECK chỉ nhận đúng tên các giá trị của enum, đặt tên CK_{Bảng}_{Cột}.</summary>
    public static void HasEnumCheck<TEnum>(this TableBuilder table, string column) where TEnum : struct, Enum =>
        table.HasCheckConstraint(
            $"CK_{table.Name}_{column}",
            $"[{column}] IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"N'{n}'"))})");

    /// <summary>Option SQL Server dùng chung cho migrator, design-time factory và test: bảng lịch sử migration nằm trong schema của module.</summary>
    public static DbContextOptions<TContext> SqlServerOptions<TContext>(string connectionString, string schema)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;

    /// <summary>Connection string cho lệnh dotnet ef lúc design-time; migrations add/script không mở kết nối.</summary>
    public static string DesignTimeConnectionString =>
        Environment.GetEnvironmentVariable("SH_MIGRATOR_CONNECTION") ?? "Server=design-time-only;Database=SmartHealthcare";
}
