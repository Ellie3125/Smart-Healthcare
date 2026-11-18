namespace Scheduling.Contracts;

public sealed record SchedulingDailyReport(DateOnly Date, int Appointments, int Cancelled, int Tickets);
