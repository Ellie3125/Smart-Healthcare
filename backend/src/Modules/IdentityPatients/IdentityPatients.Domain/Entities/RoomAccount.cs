namespace IdentityPatients.Domain.Entities;

public sealed class RoomAccount
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public Guid RoomId { get; set; }
    public string AccountCode { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
