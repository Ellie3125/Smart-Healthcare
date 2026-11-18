using Billing.Contracts;
using Clinical.Contracts;
using Scheduling.Contracts;

namespace DocumentsReporting.Application.Reports;

public sealed record ReportDay(DateOnly Date, int Bookings, int CheckedIn, decimal Revenue,
    int Tickets, decimal CashRevenue, decimal OnlineRevenue, decimal Refunded);
public sealed record ReportSnapshot(DateOnly DateFrom, DateOnly DateTo, int TotalAppointments, int CheckedInCount,
    int CancelledCount, decimal TotalRevenue, decimal TotalRefunded, IReadOnlyList<ReportDay> DailySeries,
    int TotalEncounters, int TotalTickets, decimal CashRevenue, decimal OnlineRevenue, string Currency);

public sealed class ReportSummaryService(ISchedulingModule scheduling, IClinicalModule clinical, IBillingModule billing)
{
    public async Task<ReportSnapshot> ReadAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (from == default || to < from || to.DayNumber - from.DayNumber > 30 || to == DateOnly.MaxValue)
            throw new ArgumentException("Invalid report interval.");
        var start = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var until = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var appointments = (await scheduling.GetDailyReportAsync(start, until, ct)).ToDictionary(d => d.Date);
        var encounters = (await clinical.GetDailyReportAsync(start, until, ct)).ToDictionary(d => d.Date);
        var money = (await billing.GetDailyReportAsync(start, until, ct)).ToDictionary(d => d.Date);
        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(offset =>
        {
            var date = from.AddDays(offset);
            var bookings = appointments.GetValueOrDefault(date);
            var checkins = encounters.GetValueOrDefault(date)?.Encounters ?? 0;
            var finance = money.GetValueOrDefault(date);
            var cash = finance?.CashRevenue ?? 0m;
            var online = finance?.OnlineRevenue ?? 0m;
            return new ReportDay(date, bookings?.Appointments ?? 0, checkins, cash + online, bookings?.Tickets ?? 0,
                cash, online, finance?.Refunded ?? 0m);
        }).ToArray();
        return new(from, to, days.Sum(d => d.Bookings), days.Sum(d => d.CheckedIn), appointments.Values.Sum(d => d.Cancelled),
            days.Sum(d => d.Revenue), days.Sum(d => d.Refunded), days, days.Sum(d => d.CheckedIn), days.Sum(d => d.Tickets),
            days.Sum(d => d.CashRevenue), days.Sum(d => d.OnlineRevenue), "VND");
    }
}
