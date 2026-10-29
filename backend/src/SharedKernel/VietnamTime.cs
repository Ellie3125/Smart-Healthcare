namespace SmartHealthcare.SharedKernel;

/// <summary>Mốc nghiệp vụ tính theo giờ Việt Nam; dữ liệu vẫn lưu UTC.</summary>
public static class VietnamTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);
}
