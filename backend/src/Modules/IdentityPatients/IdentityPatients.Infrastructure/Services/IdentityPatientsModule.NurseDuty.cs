using IdentityPatients.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed partial class IdentityPatientsModule
{
    public Task<bool> IsNurseOnDutyAsync(Guid appUserId, Guid roomId, DateTime atUtc, CancellationToken ct = default)
    {
        if (appUserId == Guid.Empty || roomId == Guid.Empty || atUtc.Kind != DateTimeKind.Utc)
            return Task.FromResult(false);
        var date = DateOnly.FromDateTime(atUtc.AddHours(7));
        return (from shift in _dbContext.StaffShiftAssignments.AsNoTracking()
                join staff in _dbContext.Staffs on shift.StaffId equals staff.Id
                join user in _dbContext.AppUsers on staff.AppUserId equals user.Id
                join room in _dbContext.Rooms on shift.RoomId equals room.Id
                where user.Id == appUserId && user.IsActive && staff.IsActive && staff.StaffType == StaffType.Nurse
                    && room.Id == roomId && room.IsActive && shift.ShiftDate == date
                    && shift.StartsAtUtc <= atUtc && atUtc < shift.EndsAtUtc
                select shift.Id).AnyAsync(ct);
    }
}
