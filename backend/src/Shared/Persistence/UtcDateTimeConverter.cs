using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SmartHealthcare.Shared.Persistence;

/// <summary>Lưu mọi DateTime dưới dạng UTC; giá trị đọc ra luôn có Kind = Utc.</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
