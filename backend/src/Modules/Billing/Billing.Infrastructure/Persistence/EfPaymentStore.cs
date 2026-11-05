using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using OutboxAggregateType = Shared.Infrastructure.Persistence.Enums.AggregateType;
using OutboxEventType = Shared.Infrastructure.Persistence.Enums.EventType;

namespace Billing.Infrastructure.Persistence;

internal sealed class EfPaymentStore(BillingDbContext db, OutboxWriter outbox) : IPaymentStore
{
    private const int DeadlockErrorNumber = 1205;
    private const int MaxAttempts = 3;

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        // Bên gọi đã mở transaction (unit of work của module khởi phát, hoặc test): tham gia, không commit thay họ.
        if (db.Database.CurrentTransaction is not null)
            return await work(ct);

        for (var attempt = 1; ; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await work(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsDeadlock(ex))
            {
                // DB-12: rollback rồi chạy lại TOÀN BỘ transaction (đọc lại trạng thái, kiểm lại điều kiện).
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                outbox.Reset();
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt + Random.Shared.Next(0, 50)), ct);
            }
        }
    }

    public async Task<IReadOnlyList<BillingItem>> LockChargesAsync(IReadOnlyList<Guid> chargeIds, CancellationToken ct)
    {
        var locked = new List<BillingItem>(chargeIds.Count);
        foreach (var id in chargeIds.Order())
        {
            var charge = await db.Set<BillingItem>()
                .FromSql($"SELECT * FROM [billing].[BillingItem] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .SingleOrDefaultAsync(ct);
            if (charge is not null)
                locked.Add(charge);
        }

        return locked;
    }

    public Task LockBookingAttemptAsync(Guid bookingAttemptId, CancellationToken ct) =>
        // Khóa phạm vi khóa trên index BookingAttemptId: request thứ hai chờ tới khi request đầu commit.
        db.Database.ExecuteSqlAsync(
            $"SELECT [Id] FROM [billing].[PaymentIntent] WITH (UPDLOCK, HOLDLOCK) WHERE [BookingAttemptId] = {bookingAttemptId}", ct);

    public async Task<IReadOnlyList<OpenPayment>> FindOpenPaymentsAsync(IReadOnlyCollection<Guid> chargeIds, Guid? bookingAttemptId, DateTime nowUtc, CancellationToken ct)
    {
        var ids = chargeIds.ToList();
        var rows = await (
                from transaction in db.Set<PaymentTransaction>()
                join intent in db.Set<PaymentIntent>() on transaction.PaymentIntentId equals intent.Id
                where transaction.PaymentStatus == PaymentStatus.Pending
                      && (intent.Status == PaymentIntentStatus.Pending || intent.Status == PaymentIntentStatus.Processing)
                      && intent.ExpiresAtUtc > nowUtc
                      && ((bookingAttemptId != null && intent.BookingAttemptId == bookingAttemptId)
                          || db.Set<PaymentAllocation>().Any(a => a.PaymentTransactionId == transaction.Id && ids.Contains(a.BillingItemId)))
                select new { intent, transaction })
            .ToListAsync(ct);

        var result = new List<OpenPayment>(rows.Count);
        foreach (var row in rows)
        {
            var allocated = await db.Set<PaymentAllocation>()
                .Where(a => a.PaymentTransactionId == row.transaction.Id)
                .Select(a => a.BillingItemId)
                .ToListAsync(ct);
            result.Add(new OpenPayment(row.intent, row.transaction, allocated));
        }

        return result;
    }

    public void Add(PaymentIntent intent, PaymentTransaction transaction, IReadOnlyList<PaymentAllocation> allocations)
    {
        // Add gán Id tuần tự phía client (SequentialGuidValueGenerator) nên nối khóa ngoại được ngay.
        db.Add(intent);
        transaction.PaymentIntentId = intent.Id;
        db.Add(transaction);
        foreach (var allocation in allocations)
        {
            allocation.PaymentTransactionId = transaction.Id;
            db.Add(allocation);
        }
    }

    public Task<PaymentTransaction?> LockTransactionAsync(string transactionReference, CancellationToken ct) =>
        db.Set<PaymentTransaction>()
            .FromSql($"SELECT * FROM [billing].[PaymentTransaction] WITH (UPDLOCK, HOLDLOCK) WHERE [TransactionReference] = {transactionReference}")
            .SingleOrDefaultAsync(ct);

    // HOLDLOCK khóa cả khoảng khóa trên IX_PaymentTransaction_ProviderTransactionId khi chưa có dòng nào mang mã này.
    public Task<bool> IsProviderTransactionIdTakenAsync(string providerTransactionId, Guid exceptTransactionId, CancellationToken ct) =>
        db.Set<PaymentTransaction>()
            .FromSql($"SELECT * FROM [billing].[PaymentTransaction] WITH (UPDLOCK, HOLDLOCK) WHERE [ProviderTransactionId] = {providerTransactionId}")
            .AnyAsync(t => t.Id != exceptTransactionId, ct);

    public Task<PaymentIntent> GetIntentAsync(Guid paymentIntentId, CancellationToken ct) =>
        db.Set<PaymentIntent>().SingleAsync(i => i.Id == paymentIntentId, ct);

    public async Task<IReadOnlyList<PaymentAllocation>> GetAllocationsAsync(Guid paymentTransactionId, CancellationToken ct) =>
        await db.Set<PaymentAllocation>().Where(a => a.PaymentTransactionId == paymentTransactionId).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public Task EnqueueAsync(PaymentVerified integrationEvent, CancellationToken ct) =>
        outbox.EnqueueAsync(db, integrationEvent, OutboxEventType.PaymentVerified, OutboxAggregateType.PaymentIntent, integrationEvent.PaymentIntentId, ct);

    private static bool IsDeadlock(Exception ex) => ex switch
    {
        SqlException sql => sql.Number == DeadlockErrorNumber,
        DbUpdateException { InnerException: SqlException sql } => sql.Number == DeadlockErrorNumber,
        _ => false,
    };
}
