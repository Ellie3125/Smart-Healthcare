using Billing.Application;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Billing.Infrastructure.Persistence;

internal sealed class EfCashierStore(BillingDbContext db, SharedDbContext shared, TimeProvider clock) : ICashierStore
{
    public async Task WriteCollectionAuditAsync(Guid staffUserId, Guid paymentTransactionId, string newValuesJson, string? clientIp,
        CancellationToken ct)
    {
        var transaction = (db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("AuditLog thu tiền phải ghi trong transaction của Billing.")).GetDbTransaction();

        // Dùng chung kết nối và transaction của BillingDbContext, như OutboxWriter: phiếu thu và AuditLog cùng commit hoặc cùng rollback.
        if (shared.Database.CurrentTransaction?.GetDbTransaction() != transaction)
        {
            if (shared.Database.CurrentTransaction is not null)
                await shared.Database.UseTransactionAsync(null, ct);
            if (shared.Database.GetDbConnection() != transaction.Connection)
                shared.Database.SetDbConnection(transaction.Connection, contextOwnsConnection: false);
            await shared.Database.UseTransactionAsync(transaction, ct);
        }

        await new AuditLogWriter(shared).WriteAsync(new AuditLog
        {
            PerformedByUserId = staffUserId,
            TableName = "billing.PaymentTransaction",
            RecordId = paymentTransactionId,
            ActionType = ActionType.INSERT,
            ClientIpAddress = clientIp,
            ChangedColumns = "PaymentStatus,TransactionAmount,VerifiedAtUtc",
            NewValuesJson = newValuesJson,
            ChangeReason = "Thu tiền mặt tại quầy",
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
        }, ct);
    }

    // Với Provider = Counter, PayerAppUserId là tài khoản tiếp tân đã nhận tiền (xem CashierService).
    public async Task<IReadOnlyList<CounterReceipt>> GetReceiptsAsync(Guid staffUserId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await (
                from intent in db.Set<PaymentIntent>().AsNoTracking()
                join transaction in db.Set<PaymentTransaction>().AsNoTracking() on intent.Id equals transaction.PaymentIntentId
                where intent.Provider == PaymentIntentProvider.Counter && intent.PayerAppUserId == staffUserId
                      && transaction.PaymentStatus == PaymentStatus.Success
                      && transaction.VerifiedAtUtc >= fromUtc && transaction.VerifiedAtUtc < toUtc
                orderby transaction.VerifiedAtUtc
                select new CounterReceipt(transaction.TransactionReference, intent.Id, intent.PatientId, intent.EncounterId,
                    transaction.TransactionAmount, transaction.VerifiedAtUtc!.Value))
            .ToListAsync(ct);
}
