using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartHealthcare.Identity.Application;
using SmartHealthcare.Identity.Domain;

namespace SmartHealthcare.Identity.Infrastructure.Persistence;

internal sealed class UserAccountRepository(IdentityModuleDbContext db) : IUserAccountRepository
{
    public Task<UserAccount?> FindByLoginAsync(LoginKind kind, string normalizedValue, CancellationToken ct) =>
        kind switch
        {
            LoginKind.Email => db.Accounts.SingleOrDefaultAsync(a => a.Email == normalizedValue, ct),
            LoginKind.PhoneNumber => db.Accounts.SingleOrDefaultAsync(a => a.PhoneNumber == normalizedValue, ct),
            _ => db.Accounts.SingleOrDefaultAsync(a => a.UserName == normalizedValue, ct),
        };

    public Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<Staff?> FindStaffAsync(Guid appUserId, CancellationToken ct) =>
        db.Staff.AsNoTracking().SingleOrDefaultAsync(s => s.AppUserId == appUserId, ct);

    public async Task<bool> TryAddAsync(UserAccount account, CancellationToken ct)
    {
        db.Accounts.Add(account);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(account).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
