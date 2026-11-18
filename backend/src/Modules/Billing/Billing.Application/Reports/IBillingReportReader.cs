using Billing.Contracts;

namespace Billing.Application.Reports;

public interface IBillingReportReader
{
    Task<IReadOnlyList<BillingDailyReport>> ReadAsync(DateTime fromUtc, DateTime untilUtc, CancellationToken ct);
}
