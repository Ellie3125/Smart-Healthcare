namespace SmartHealthcare.Billing.Domain;

public sealed class PaymentIntent
{
    public Guid Id { get; set; }
    public string IntentNumber { get; set; } = null!;
    public Guid PayerAppUserId { get; set; }
    public Guid PatientId { get; set; }
    public Guid? BookingAttemptId { get; set; }
    public Guid? EncounterId { get; set; }
    public decimal TotalExpectedAmount { get; set; }
    public Currency Currency { get; set; }
    public PaymentIntentProvider Provider { get; set; }
    public PaymentType PaymentType { get; set; }
    public PaymentIntentStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
