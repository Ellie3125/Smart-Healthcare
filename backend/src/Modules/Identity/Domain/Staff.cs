namespace SmartHealthcare.Identity.Domain;

public enum StaffType
{
    Doctor,
    Nurse,
    Receptionist,
    Technician,
}

/// <summary>Hồ sơ nhân viên gắn 1–0..1 với tài khoản. FR01 chỉ đọc bảng này để xác định vai trò khi đăng nhập.</summary>
public sealed class Staff
{
    private Staff()
    {
    }

    public Guid Id { get; private set; }
    public Guid AppUserId { get; private set; }
    public string StaffCode { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public StaffType StaffType { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
