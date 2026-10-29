namespace SmartHealthcare.SharedKernel;

public static class TimeProviderExtensions
{
    /// <summary>Giờ UTC làm tròn xuống mili-giây, khớp cột datetime2(3), để giá trị trả về ngay sau khi tạo bằng giá trị đọc lại từ DB.</summary>
    public static DateTime GetUtcDateTime(this TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
    }
}
