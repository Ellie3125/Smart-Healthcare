using Notifications.Domain.Enums;

namespace Notifications.Domain.Entities;

public sealed class PatientNotification
{
    public Guid Id { get; set; }
    public Guid SourceEventId { get; set; }
    // Cross-module identity.Patient ID; no foreign key.
    public Guid PatientId { get; set; }
    public PatientNotificationKind Kind { get; set; }
    public Guid TargetEntityId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
