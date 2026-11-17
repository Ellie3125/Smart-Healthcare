using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence;

internal sealed class EfReconciliationStore(BillingDbContext db) : IReconciliationStore
{
    public async Task<IReadOnlyList<string>> FindOverdueAsync(DateTime expiredBeforeUtc, int take, CancellationToken ct) =>
        await (
                from intent in db.Set<PaymentIntent>().AsNoTracking()
                join transaction in db.Set<PaymentTransaction>().AsNoTracking() on intent.Id equals transaction.PaymentIntentId
                where intent.Provider == PaymentIntentProvider.VNPAY
                      && (intent.Status == PaymentIntentStatus.Pending || intent.Status == PaymentIntentStatus.Processing)
                      && transaction.PaymentStatus == PaymentStatus.Pending
                      && intent.ExpiresAtUtc <= expiredBeforeUtc
                orderby intent.ExpiresAtUtc
                select transaction.TransactionReference)
            .Take(take)
            .ToListAsync(ct);

    public async Task<(PaymentIntent Intent, PaymentTransaction Transaction)?> FindByReferenceAsync(string transactionReference, CancellationToken ct)
    {
        var row = await (
                from transaction in db.Set<PaymentTransaction>().AsNoTracking()
                join intent in db.Set<PaymentIntent>().AsNoTracking() on transaction.PaymentIntentId equals intent.Id
                where transaction.TransactionReference == transactionReference
                select new { intent, transaction })
            .SingleOrDefaultAsync(ct);
        return row is null ? null : (row.intent, row.transaction);
    }

    public async Task<IReadOnlyList<PaymentNeedingReview>> ListNeedingReviewAsync(int take, CancellationToken ct) =>
        await (
                from intent in db.Set<PaymentIntent>().AsNoTracking()
                join transaction in db.Set<PaymentTransaction>().AsNoTracking() on intent.Id equals transaction.PaymentIntentId
                where intent.Provider == PaymentIntentProvider.VNPAY && intent.Status == PaymentIntentStatus.Unknown
                orderby intent.CreatedAtUtc descending
                select new PaymentNeedingReview(
                    transaction.TransactionReference,
                    intent.Id,
                    intent.IntentNumber,
                    intent.PaymentType == PaymentType.InitialConsultation ? PaymentPurpose.InitialConsultation : PaymentPurpose.CLSOrderBatch,
                    intent.PatientId,
                    transaction.TransactionAmount,
                    intent.CreatedAtUtc,
                    intent.ExpiresAtUtc))
            .Take(take)
            .ToListAsync(ct);
}
