using Clinical.Application.Vitals;
using IdentityPatients.Contracts;

namespace Clinical.Api;

internal sealed class IdentityNurseStaffDirectory(IIdentityPatientsModule identity) : INurseStaffDirectory
{
    public async Task<Guid?> GetActiveNurseStaffIdAsync(Guid appUserId, CancellationToken ct)
    {
        var staff = await identity.GetActiveStaffAsync(appUserId, ct);
        return staff?.StaffType == Roles.Nurse ? staff.StaffId : null;
    }
}
