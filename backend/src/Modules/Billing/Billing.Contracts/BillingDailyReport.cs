namespace Billing.Contracts;

public sealed record BillingDailyReport(DateOnly Date, decimal CashRevenue, decimal OnlineRevenue, decimal Refunded);
