using Clinical.Domain.Enums;

namespace Clinical.Domain.Entities;

public sealed class ServiceOrder
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public int OrderRoundNumber { get; set; }
    public DateTime OrderedAtUtc { get; set; }
    // ID chéo module: identity.Doctor, không FK
    public Guid PrescribedByDoctorId { get; set; }
    public OrderBatchStatus OrderBatchStatus { get; set; }
    public bool HasActiveReturnTicket { get; set; }
}
