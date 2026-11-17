using Microsoft.EntityFrameworkCore;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// Dọn dữ liệu kỹ thuật đã hết dùng (trưởng nhóm chốt 17/11/2026: giữ 30 ngày), chạy hằng ngày trong Worker:
/// OutboxMessage Published, ProcessedEvent cũ, ApiIdempotencyRecord đã hết hạn. Không đụng AuditLog hay dữ liệu nghiệp vụ.
/// Xóa theo lô để không giữ khóa lâu; mốc giờ lấy từ DB.
/// </summary>
public sealed class TechnicalDataCleanupJob(SharedDbContext shared)
{
    public const int RetentionDays = 30;
    private const int BatchSize = 1000;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var total = 0;
        total += await DeleteInBatchesAsync($"""
            DELETE TOP ({BatchSize}) FROM shared.OutboxMessage
            WHERE Status = N'Published' AND ProcessedAtUtc < DATEADD(DAY, -{RetentionDays}, SYSUTCDATETIME())
            """, ct);
        // Giữ ProcessedEvent của sự kiện còn chưa Published (DeadLetter, Pending): retry tay sau này không chạy lại handler đã xong.
        total += await DeleteInBatchesAsync($"""
            DELETE TOP ({BatchSize}) FROM shared.ProcessedEvent
            WHERE ProcessedAtUtc < DATEADD(DAY, -{RetentionDays}, SYSUTCDATETIME())
              AND NOT EXISTS (SELECT 1 FROM shared.OutboxMessage o WHERE o.Id = ProcessedEvent.EventId AND o.Status <> N'Published')
            """, ct);
        // Một ngày sau hạn: request gửi lặp sát lúc hết hạn vẫn trả kết quả cũ.
        total += await DeleteInBatchesAsync($"""
            DELETE TOP ({BatchSize}) FROM shared.ApiIdempotencyRecord
            WHERE ExpiresAtUtc < DATEADD(DAY, -1, SYSUTCDATETIME())
            """, ct);
        return total;
    }

    private async Task<int> DeleteInBatchesAsync(string sql, CancellationToken ct)
    {
        var total = 0;
        int deleted;
        do
        {
            // Câu SQL chỉ ghép hằng số của lớp này, không có dữ liệu ngoài.
#pragma warning disable EF1002
            deleted = await shared.Database.ExecuteSqlRawAsync(sql, ct);
#pragma warning restore EF1002
            total += deleted;
        } while (deleted == BatchSize && !ct.IsCancellationRequested);
        return total;
    }
}
