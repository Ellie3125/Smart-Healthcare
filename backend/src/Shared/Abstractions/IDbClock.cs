namespace Shared.Abstractions;

/// <summary>
/// Trừu tượng hóa đồng hồ DB để test và để đọc giờ UTC từ DB sau khi nhận khóa.
/// TODO: Implement thật khi cần đọc GETUTCDATE() từ DB trong transaction (DB-08).
/// </summary>
public interface IDbClock
{
    Task<DateTime> GetUtcNowAsync(CancellationToken ct = default);
}

