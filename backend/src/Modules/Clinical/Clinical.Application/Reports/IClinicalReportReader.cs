using Clinical.Contracts;

namespace Clinical.Application.Reports;

public interface IClinicalReportReader
{
    Task<IReadOnlyList<ClinicalDailyReport>> ReadAsync(DateTime fromUtc, DateTime untilUtc, CancellationToken ct);
}
