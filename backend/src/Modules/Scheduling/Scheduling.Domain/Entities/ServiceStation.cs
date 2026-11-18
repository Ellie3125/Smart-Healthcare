namespace Scheduling.Domain.Entities;

public sealed class ServiceStation
{
    public Guid Id { get; set; }
    // ID chéo module: identity.Room, không FK
    public Guid RoomId { get; set; }
    public string StationCode { get; set; } = null!;
    public string StationName { get; set; } = null!;
    public bool IsActive { get; set; }
}
