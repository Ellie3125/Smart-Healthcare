using Billing.Application.Reports;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Reports;

internal sealed class EfBillingReportReader(BillingDbContext db) : IBillingReportReader
{
    public async Task<IReadOnlyList<BillingDailyReport>> ReadAsync(DateTime fromUtc, DateTime untilUtc, CancellationToken ct)
    {
        var receipts = await (from transaction in db.Set<PaymentTransaction>().AsNoTracking()
                              join intent in db.Set<PaymentIntent>().AsNoTracking() on transaction.PaymentIntentId equals intent.Id
                              where transaction.PaymentStatus == PaymentStatus.Success && transaction.Currency == "VND"
                                  && transaction.VerifiedAtUtc >= fromUtc && transaction.VerifiedAtUtc < untilUtc
                              group new { transaction.TransactionAmount, intent.Provider } by transaction.VerifiedAtUtc!.Value.AddHours(7).Date into day
                              select new BillingDailyReport(DateOnly.FromDateTime(day.Key),
                                  day.Sum(p => p.Provider == PaymentIntentProvider.Counter ? p.TransactionAmount : 0m),
                                  day.Sum(p => p.Provider == PaymentIntentProvider.VNPAY ? p.TransactionAmount : 0m), 0m)).ToListAsync(ct);
        var refunds = await db.Set<RefundTransaction>().AsNoTracking()
            .Where(r => r.Status == RefundTransactionStatus.Success && r.Currency == "VND"
                && r.ProcessedAtUtc >= fromUtc && r.ProcessedAtUtc < untilUtc)
            .GroupBy(r => r.ProcessedAtUtc.AddHours(7).Date)
            .Select(g => new BillingDailyReport(DateOnly.FromDateTime(g.Key), 0m, 0m, g.Sum(r => r.RefundedAmount))).ToListAsync(ct);
        return receipts.Concat(refunds).GroupBy(d => d.Date).OrderBy(g => g.Key)
            .Select(g => new BillingDailyReport(g.Key, g.Sum(d => d.CashRevenue), g.Sum(d => d.OnlineRevenue), g.Sum(d => d.Refunded))).ToArray();
    }
}
