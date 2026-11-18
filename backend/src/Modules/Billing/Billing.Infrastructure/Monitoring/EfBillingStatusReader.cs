using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Monitoring;

internal sealed class EfBillingStatusReader(BillingDbContext db) : IBillingStatusReader
{
    public async Task<PaidUnallocatedPage> GetPaidUnallocatedAsync(Guid afterRefundRequestId, int take, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, 100);
        // PK keyset in SQL Server GUID order; do not translate Guid.CompareTo into a non-sargable CASE expression.
        var rows = await db.Database.SqlQuery<PaidUnallocatedItem>($"""
            SELECT TOP({take + 1}) r.Id AS RefundRequestId, t.PaymentIntentId, p.BookingAttemptId,
                r.RequestedAmount, r.Status, r.RequestedAtUtc
            FROM billing.RefundRequest r
            JOIN billing.PaymentTransaction t ON t.Id = r.OriginalPaymentTransactionId
            JOIN billing.PaymentIntent p ON p.Id = t.PaymentIntentId
            WHERE r.Id > {afterRefundRequestId} AND r.RefundReason = N'PaidUnallocated' AND r.Status <> N'Settled'
            ORDER BY r.Id
            """).ToListAsync(ct);
        var items = rows.Take(take).Select(x => x with { RequestedAtUtc = DateTime.SpecifyKind(x.RequestedAtUtc, DateTimeKind.Utc) }).ToArray();
        var hasMore = rows.Count > take;
        return new(items, hasMore, hasMore ? items[^1].RefundRequestId : null);
    }

    public async Task<BookingPaymentState> GetBookingPaymentStatusAsync(Guid bookingAttemptId, CancellationToken ct)
    {
        if (bookingAttemptId == Guid.Empty) throw new ArgumentException("BookingAttemptId không được rỗng.", nameof(bookingAttemptId));
        var intents = await db.Set<PaymentIntent>().AsNoTracking()
            .Where(p => p.BookingAttemptId == bookingAttemptId && p.PaymentType == PaymentType.InitialConsultation)
            .Select(p => new
            {
                p.Status,
                Expected = p.TotalExpectedAmount,
                Verified = db.Set<PaymentTransaction>().Any(t => t.PaymentIntentId == p.Id && t.PaymentStatus == PaymentStatus.Success && t.VerifiedAtUtc != null),
                Paid = db.Set<PaymentTransaction>().Where(t => t.PaymentIntentId == p.Id && t.PaymentStatus == PaymentStatus.Success && t.VerifiedAtUtc != null)
                    .Sum(t => (decimal?)t.TransactionAmount) ?? 0,
                Refunded = db.Set<RefundTransaction>().Where(r => r.Status == RefundTransactionStatus.Success
                    && db.Set<PaymentTransaction>().Any(t => t.Id == r.OriginalPaymentTransactionId && t.PaymentIntentId == p.Id))
                    .Sum(r => (decimal?)r.RefundedAmount) ?? 0,
                RefundPending = db.Set<RefundRequest>().Any(r => (r.Status == RefundRequestStatus.Pending || r.Status == RefundRequestStatus.Approved || r.Status == RefundRequestStatus.Failed)
                    && db.Set<PaymentTransaction>().Any(t => t.Id == r.OriginalPaymentTransactionId && t.PaymentIntentId == p.Id))
            }).ToListAsync(ct);
        // A refunded duplicate must not hide another successful payment retained for this booking.
        var status = intents.Any(x => x.Verified && x.Paid >= x.Expected && (x.Paid > x.Refunded || x.Expected == 0) && !x.RefundPending) ? BookingPaymentStatus.Paid
            : intents.Any(x => x.Paid > 0 && x.RefundPending) ? BookingPaymentStatus.RefundPending
            : intents.Any(x => x.Paid > 0) && intents.Where(x => x.Paid > 0).All(x => x.Refunded >= x.Paid) ? BookingPaymentStatus.Refunded
            : intents.Count == 0 || intents.Any(x => x.Status is PaymentIntentStatus.Unknown or PaymentIntentStatus.Succeeded) ? BookingPaymentStatus.Unknown
            : intents.Any(x => x.Status is PaymentIntentStatus.Pending or PaymentIntentStatus.Processing) ? BookingPaymentStatus.Pending
            : BookingPaymentStatus.Failed;
        return new(bookingAttemptId, status);
    }
}
