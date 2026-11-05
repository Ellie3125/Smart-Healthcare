namespace Shared.Abstractions;

/// <summary>
/// Giờ UTC của SQL Server (SYSUTCDATETIME), đọc trong transaction đang mở.
/// Dùng cho mọi điều kiện nghiệp vụ theo giờ (cutoff đặt lịch, hết hạn giữ chỗ, hạn check in, hạn thanh toán)
/// để BE1, BE2 và worker cùng quyết định theo một đồng hồ (Giải pháp v2.11 §1.2).
/// </summary>
/// <remarks>
/// Gọi SAU khi đã khóa hàng bảo vệ (ví dụ DoctorDayQuota WITH (UPDLOCK, HOLDLOCK)):
/// đọc trước khi chờ khóa thì request chờ lâu có thể lọt cutoff.
/// Trong test thay bằng đồng hồ giả. Dấu thời gian chỉ để ghi log có thể dùng TimeProvider.
/// </remarks>
public interface IDbClock
{
    Task<DateTime> GetUtcNowAsync(CancellationToken ct);
}
