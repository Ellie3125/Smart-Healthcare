namespace SmartHealthcare.Identity.Domain;

/// <summary>Tài khoản đăng nhập (bảng dbo.AppUser theo ERD v2.2).</summary>
public sealed class UserAccount
{
    private UserAccount()
    {
    }

    public Guid Id { get; private set; }
    public string UserName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string PhoneNumber { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public bool EmailConfirmed { get; private set; }
    public bool PhoneNumberConfirmed { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Tài khoản bệnh nhân tự đăng ký; giá trị đầu vào đã được chuẩn hóa và kiểm tra.</summary>
    public static UserAccount Register(string userName, string email, string phoneNumber, string passwordHash, DateTime nowUtc) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserName = userName,
            Email = email,
            PhoneNumber = phoneNumber,
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAtUtc = nowUtc,
        };

    public void ReplacePasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
