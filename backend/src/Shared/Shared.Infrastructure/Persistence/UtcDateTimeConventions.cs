using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Shared.Infrastructure.Persistence;

public static class UtcDateTimeConventions
{
    // Mọi cột datetime2 lưu UTC: đọc ra gán Kind = Utc, ghi vào chỉ nhận Kind = Utc.
    public static ModelConfigurationBuilder UseUtcDateTime(this ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
        return builder;
    }
}

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => EnsureUtc(value),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    public static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : throw new ArgumentException($"Giá trị DateTime phải là UTC (Kind = Utc), nhận Kind = {value.Kind}.", nameof(value));
}
