using Scheduling.Domain.Enums;

namespace Scheduling.Domain.Entities;

public sealed class MedicalService
{
    public Guid Id { get; set; }
    public string ServiceCode { get; set; } = null!;
    public string ServiceName { get; set; } = null!;
    public ServiceCategory ServiceCategory { get; set; }
    // ID chéo module: identity.Room, không FK
    public Guid DefaultRoomId { get; set; }
    public bool IsMandatoryDefault { get; set; }
    public bool IsActive { get; set; }
}
