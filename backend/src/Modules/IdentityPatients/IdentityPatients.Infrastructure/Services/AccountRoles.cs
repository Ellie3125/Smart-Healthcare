using IdentityPatients.Contracts;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

/// <summary>Vai trò đăng nhập suy được của một AppUser (không lưu cột vai trò, ERD v2.4; Giải pháp v2.14 "Kênh và vai trò").</summary>
public sealed record AccountRole(string Role, Guid? DoctorId, Guid? RoomId, Guid? RoomAccountId)
{
    public string Channel => Role == Roles.Patient ? Channels.Patient : Channels.Staff;
}

/// <summary>Hàm duy nhất suy vai trò: có RoomAccount → RoomAccount; có Staff → StaffType; không có cả hai → Patient.</summary>
public static class AccountRoles
{
    /// <returns>Tên vai trò, hoặc null khi tài khoản không có vai trò đăng nhập (Technician).</returns>
    public static string? RoleFor(bool hasRoomAccount, StaffType? staffType) =>
        hasRoomAccount ? Roles.RoomAccount : staffType switch
        {
            null => Roles.Patient,
            StaffType.Doctor => Roles.Doctor,
            StaffType.Nurse => Roles.Nurse,
            StaffType.Receptionist => Roles.Receptionist,
            StaffType.Admin => Roles.Admin,
            _ => null
        };

    /// <returns>null khi tài khoản không đăng nhập được: Technician, hoặc Staff/RoomAccount đã bị vô hiệu hóa.</returns>
    public static async Task<AccountRole?> ResolveAsync(IdentityPatientsDbContext db, Guid appUserId, CancellationToken ct)
    {
        var roomAccount = await db.RoomAccounts.AsNoTracking()
            .Where(r => r.AppUserId == appUserId)
            .Select(r => new { r.Id, r.RoomId, r.IsActive })
            .SingleOrDefaultAsync(ct);
        if (roomAccount is not null)
        {
            return roomAccount.IsActive ? new AccountRole(Roles.RoomAccount, null, roomAccount.RoomId, roomAccount.Id) : null;
        }

        var staff = await db.Staffs.AsNoTracking()
            .Where(s => s.AppUserId == appUserId)
            .Select(s => new { s.Id, s.StaffType, s.IsActive })
            .SingleOrDefaultAsync(ct);
        if (staff is not null && !staff.IsActive)
        {
            return null;
        }

        var role = RoleFor(false, staff?.StaffType);
        if (role is null)
        {
            return null;
        }

        Guid? doctorId = role == Roles.Doctor
            ? await db.Doctors.AsNoTracking().Where(d => d.StaffId == staff!.Id).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct)
            : null;
        return new AccountRole(role, doctorId, null, null);
    }
}
