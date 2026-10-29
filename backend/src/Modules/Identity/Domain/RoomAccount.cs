namespace SmartHealthcare.Identity.Domain;

public sealed class RoomAccount
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public string AccountCode { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
