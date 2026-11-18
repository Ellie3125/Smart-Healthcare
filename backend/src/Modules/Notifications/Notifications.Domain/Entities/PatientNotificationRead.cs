namespace Notifications.Domain.Entities;

public sealed class PatientNotificationRead
{
    public Guid NotificationId { get; set; }
    // Cross-module identity.AppUser ID; each representative has an independent read state.
    public Guid AppUserId { get; set; }
    public DateTime ReadAtUtc { get; set; }
}
