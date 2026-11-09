using Notifications.Domain.Enums;



namespace Notifications.Domain.Entities;



public sealed class Operation

{

    public Guid Id { get; set; }

    // ID chéo module: identity.AspNetUsers, không FK

    public Guid InitiatedByUserId { get; set; }

    // ID chéo module: scheduling.BookingAttempt, không FK

    public Guid? BookingAttemptId { get; set; }

    public OperationType OperationType { get; set; }

    public TargetEntityType TargetEntityType { get; set; }

    public Guid TargetEntityId { get; set; }

    public OperationStatus Status { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

}

