using Billing.Domain.Enums;

namespace Billing.Domain.Entities;

public sealed class PaymentIntent
{
    public Guid Id { get; set; }
    public string IntentNumber { get; set; } = null!;
    // ID chéo module: identity.AspNetUsers, không FK
    public Guid PayerAppUserId { get; set; }
    // ID chéo module: identity.Patient, không FK
    public Guid PatientId { get; set; }
    // ID chéo module: scheduling.BookingAttempt, không FK
    public Guid? BookingAttemptId { get; set; }
    // ID chéo module: clinical.Encounter, không FK
    public Guid? EncounterId { get; set; }
    public decimal TotalExpectedAmount { get; set; }
    public string Currency { get; set; } = null!;
    public PaymentIntentProvider Provider { get; set; }
    public PaymentType PaymentType { get; set; }
    public PaymentIntentStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
