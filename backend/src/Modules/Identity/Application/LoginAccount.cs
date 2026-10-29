using SmartHealthcare.Identity.Contracts;
using SmartHealthcare.Identity.Domain;
using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Identity.Application;

public sealed record LoginCommand(string Login, string Password);

public sealed record LoginResult(Guid UserId, string Role, string AccessToken, DateTime ExpiresAtUtc);

/// <summary>Đăng nhập bằng tên đăng nhập, email hoặc số điện thoại. Mọi lỗi xác thực trả cùng một thông báo.</summary>
public sealed class LoginAccount(IUserAccountRepository accounts, IPasswordHasher hasher, IAccessTokenIssuer tokens)
{
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("invalid_credentials", "Thông tin đăng nhập không đúng.");

    public async Task<Result<LoginResult>> ExecuteAsync(LoginCommand command, CancellationToken ct)
    {
        var (kind, value) = Classify(command.Login);
        var account = await accounts.FindByLoginAsync(kind, value, ct);
        if (account is null)
        {
            hasher.VerifyAgainstDummy(command.Password);
            return InvalidCredentials;
        }

        var check = hasher.Verify(account.PasswordHash, command.Password);
        if (check == PasswordCheck.Failed || !account.IsActive) return InvalidCredentials;

        var staff = await accounts.FindStaffAsync(account.Id, ct);
        if (staff is { IsActive: false }) return InvalidCredentials;

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            account.ReplacePasswordHash(hasher.Hash(command.Password));
            await accounts.SaveChangesAsync(ct);
        }

        var role = AccountRole.Of(staff);
        var token = tokens.Issue(account.Id, role);
        return new LoginResult(account.Id, role, token.Value, token.ExpiresAtUtc);
    }

    private static (LoginKind, string) Classify(string login)
    {
        if (login.Contains('@')) return (LoginKind.Email, AccountIdentifiers.NormalizeEmail(login));
        var phone = AccountIdentifiers.NormalizePhone(login);
        if (AccountIdentifiers.IsValidPhone(phone)) return (LoginKind.PhoneNumber, phone);
        return (LoginKind.UserName, AccountIdentifiers.NormalizeUserName(login));
    }
}

internal static class AccountRole
{
    /// <summary>Không có hồ sơ nhân viên thì là tài khoản bệnh nhân/người đại diện.</summary>
    public static string Of(Staff? staff) => staff is null ? AccountRoles.Patient : staff.StaffType.ToString();
}
