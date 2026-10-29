namespace SmartHealthcare.Scheduling.Domain;

public sealed class ServiceStation
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string StationCode { get; set; } = null!;
    public string StationName { get; set; } = null!;
    public bool IsActive { get; set; }
}
