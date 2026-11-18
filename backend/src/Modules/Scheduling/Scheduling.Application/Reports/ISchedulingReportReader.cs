using Scheduling.Contracts;

namespace Scheduling.Application.Reports;

public interface ISchedulingReportReader
{
    Task<IReadOnlyList<SchedulingDailyReport>> ReadAsync(DateTime fromUtc, DateTime untilUtc, CancellationToken ct);
}
