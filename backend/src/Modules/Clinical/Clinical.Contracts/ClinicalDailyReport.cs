namespace Clinical.Contracts;

public sealed record ClinicalDailyReport(DateOnly Date, int Encounters);
