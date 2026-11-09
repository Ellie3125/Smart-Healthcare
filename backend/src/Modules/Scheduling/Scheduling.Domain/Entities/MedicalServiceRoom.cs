namespace Scheduling.Domain.Entities;

public sealed class MedicalServiceRoom
{
    public Guid Id { get; set; }
    public Guid MedicalServiceId { get; set; }
    // ID chéo module: identity.Room, không FK
    public Guid RoomId { get; set; }
    public bool IsDefaultRoom { get; set; }
    public bool IsActive { get; set; }
}
