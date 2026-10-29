namespace SmartHealthcare.Identity.Domain;

public sealed class RoomDeviceSession
{
    public Guid Id { get; set; }
    public Guid RoomAccountId { get; set; }
    public RoomAccount RoomAccount { get; set; } = null!;
    public string DeviceId { get; set; } = null!;
    public string DeviceIpAddress { get; set; } = null!;
    /// <summary>SHA-256 (hex) của token phiên; không lưu token gốc.</summary>
    public string SessionToken { get; set; } = null!;
    public DateTime LoginAtUtc { get; set; }
    public DateTime? LogoutAtUtc { get; set; }
    public bool IsActive { get; set; }
}
