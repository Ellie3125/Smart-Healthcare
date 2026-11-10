using Microsoft.EntityFrameworkCore;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence;

/// <summary>
/// Khóa bi quan trên hàng DoctorDayQuota (Giải pháp v2.14 §1.1). Mọi đường ghi (đóng/mở ngày, giữ chỗ, hết hạn,
/// xác nhận, đổi/hủy) dùng chung truy vấn này trước khi đọc IsOpen, cutoff và bộ đếm. Phải gọi trong transaction.
/// </summary>
internal static class QuotaLock
{
    public static Task<DoctorDayQuota?> LockQuotaAsync(this SchedulingDbContext db, Guid doctorId, DateOnly serviceDate,
        CancellationToken ct) =>
        db.Set<DoctorDayQuota>()
            .FromSql($"""
                SELECT * FROM [scheduling].[DoctorDayQuota] WITH (UPDLOCK, HOLDLOCK)
                WHERE [DoctorId] = {doctorId} AND [ServiceDate] = {serviceDate}
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(ct);
}
