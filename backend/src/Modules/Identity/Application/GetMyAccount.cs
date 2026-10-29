using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Identity.Application;

public sealed record AccountView(
    Guid Id,
    string UserName,
    string Email,
    string PhoneNumber,
    bool EmailConfirmed,
    bool PhoneNumberConfirmed,
    string Role,
    DateTime CreatedAtUtc);

public sealed class GetMyAccount(IUserAccountRepository accounts)
{
    public async Task<Result<AccountView>> ExecuteAsync(Guid userId, CancellationToken ct)
    {
        var account = await accounts.FindByIdAsync(userId, ct);
        if (account is null || !account.IsActive)
            return Error.NotFound("account_not_found", "Không tìm thấy tài khoản.");

        var staff = await accounts.FindStaffAsync(account.Id, ct);
        return new AccountView(
            account.Id,
            account.UserName,
            account.Email,
            account.PhoneNumber,
            account.EmailConfirmed,
            account.PhoneNumberConfirmed,
            AccountRole.Of(staff),
            account.CreatedAtUtc);
    }
}
