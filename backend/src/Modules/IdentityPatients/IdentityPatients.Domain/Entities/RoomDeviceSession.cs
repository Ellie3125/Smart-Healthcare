namespace IdentityPatients.Domain.Entities;

public sealed class RoomDeviceSession
{
    public Guid Id { get; set; }
    public Guid RoomAccountId { get; set; }
    public string DeviceId { get; set; } = null!;
    public string DeviceIpAddress { get; set; } = null!;
    // Chỉ lưu hash SHA-256 (hex, 64 ký tự) của token phiên, không lưu token gốc
    public string SessionToken { get; set; } = null!;
    public DateTime LoginAtUtc { get; set; }
    public DateTime? LogoutAtUtc { get; set; }
    public bool IsActive { get; set; }
}
