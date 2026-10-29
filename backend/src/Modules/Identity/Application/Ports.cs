using SmartHealthcare.Identity.Domain;

namespace SmartHealthcare.Identity.Application;

public enum LoginKind
{
    UserName,
    Email,
    PhoneNumber,
}

public interface IUserAccountRepository
{
    Task<UserAccount?> FindByLoginAsync(LoginKind kind, string normalizedValue, CancellationToken ct);

    Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken ct);

    Task<Staff?> FindStaffAsync(Guid appUserId, CancellationToken ct);

    /// <summary>Thêm tài khoản; trả false khi trùng UserName, Email hoặc PhoneNumber (unique index chặn cả khi hai request chạy song song).</summary>
    Task<bool> TryAddAsync(UserAccount account, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string password);

    /// <summary>Chạy một lần kiểm tra giả khi không tìm thấy tài khoản, để thời gian phản hồi không lộ tài khoản có tồn tại hay không.</summary>
    void VerifyAgainstDummy(string password);
}

public sealed record IssuedToken(string Value, DateTime ExpiresAtUtc);

public interface IAccessTokenIssuer
{
    IssuedToken Issue(Guid userId, string role);
}
