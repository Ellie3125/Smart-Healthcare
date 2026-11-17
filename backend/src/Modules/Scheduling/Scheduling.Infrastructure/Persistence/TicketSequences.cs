using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Infrastructure.Persistence;

/// <summary>Cấp số vé từ bảng đếm <c>scheduling.TicketSequence</c> (thay MAX theo tiền tố + sp_getapplock).</summary>
internal static class TicketSequences
{
    /// <summary>
    /// Giữ <paramref name="count"/> số liên tiếp của <paramref name="scope"/>, trả số đầu tiên. Phải chạy trong transaction:
    /// khóa ghi trên dòng đếm giữ tới commit nên hai lệnh cùng phạm vi lần lượt nhận số khác nhau; rollback trả lại số.
    /// </summary>
    public static async Task<int> NextAsync(this SchedulingDbContext db, string scope, int count, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Cấp số vé phải chạy bên trong IUnitOfWork.");
        for (var attempt = 0; ; attempt++)
        {
            var last = await db.Database.SqlQuery<int>($"""
                UPDATE scheduling.TicketSequence WITH (ROWLOCK) SET LastValue = LastValue + {count}
                OUTPUT inserted.LastValue AS [Value] WHERE Scope = {scope}
                """).ToListAsync(ct);
            if (last.Count == 1)
                return last[0] - count + 1;
            try
            {
                await db.Database.ExecuteSqlAsync(
                    $"INSERT INTO scheduling.TicketSequence (Scope, LastValue) VALUES ({scope}, {count})", ct);
                return 1;
            }
            // Lệnh khác vừa tạo dòng đếm cùng phạm vi: quay lại tăng dòng đó.
            catch (SqlException e) when (e.Number is 2627 or 2601 && attempt == 0)
            {
            }
        }
    }
}
