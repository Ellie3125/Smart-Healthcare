using SmartHealthcare.Identity.Domain;
using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Identity.Application;

public sealed record RegisterAccountCommand(string UserName, string Email, string PhoneNumber, string Password);

/// <summary>Bệnh nhân tự đăng ký; bắt buộc có cả email và số điện thoại.</summary>
public sealed class RegisterAccount(IUserAccountRepository accounts, IPasswordHasher hasher, TimeProvider clock)
{
    public async Task<Result<Guid>> ExecuteAsync(RegisterAccountCommand command, CancellationToken ct)
    {
        var userName = AccountIdentifiers.NormalizeUserName(command.UserName);
        var email = AccountIdentifiers.NormalizeEmail(command.Email);
        var phone = AccountIdentifiers.NormalizePhone(command.PhoneNumber);

        var errors = new Dictionary<string, string[]>();
        if (!AccountIdentifiers.IsValidUserName(userName))
            errors["userName"] = ["Tên đăng nhập 3–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm, gạch dưới, gạch ngang."];
        if (!AccountIdentifiers.IsValidEmail(email))
            errors["email"] = ["Email không hợp lệ."];
        if (!AccountIdentifiers.IsValidPhone(phone))
            errors["phoneNumber"] = ["Số điện thoại cần 10 chữ số, bắt đầu bằng 0 hoặc +84."];
        if (PasswordPolicy.Check(command.Password) is { } passwordError)
            errors["password"] = [passwordError];
        if (errors.Count > 0) return Error.Validation(errors);

        var account = UserAccount.Register(userName, email, phone, hasher.Hash(command.Password), clock.GetUtcDateTime());
        if (!await accounts.TryAddAsync(account, ct))
            return Error.Conflict("account_already_exists", "Tên đăng nhập, email hoặc số điện thoại đã được dùng.");

        return account.Id;
    }
}
