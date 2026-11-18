using Billing.Contracts;
using Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence;

internal static class CashierShiftGuard
{
    public static Task<CashierShiftClosure?> LockAsync(BillingDbContext db, Guid actor, DateOnly date, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Cashier shift guard requires a transaction.");
        return db.Set<CashierShiftClosure>().FromSql($"""
            SELECT * FROM billing.CashierShiftClosure WITH (UPDLOCK,HOLDLOCK)
            WHERE CashierUserId={actor} AND ShiftDate={date}
            """).AsNoTracking().SingleOrDefaultAsync(ct);
    }

    public static async Task EnsureOpenAsync(BillingDbContext db, Guid actor, DateOnly date, CancellationToken ct)
    {
        if (await LockAsync(db, actor, date, ct) is not null)
            throw new BillingException(BillingErrorCode.CashierShiftClosed, "Ngày thu ngân đã được chốt.");
    }
}
