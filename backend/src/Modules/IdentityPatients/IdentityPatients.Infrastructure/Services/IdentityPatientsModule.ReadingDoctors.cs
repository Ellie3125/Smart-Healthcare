using IdentityPatients.Contracts;
using IdentityPatients.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed partial class IdentityPatientsModule
{
    public async Task<IReadOnlyList<RoomReadingDoctorDto>> GetRoomReadingDoctorsAsync(Guid roomId, DateTime atUtc, CancellationToken ct = default)
    {
        if (roomId == Guid.Empty || atUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A room and a UTC instant are required.");
        var date = DateOnly.FromDateTime(atUtc.AddHours(7));
        return await (from a in _dbContext.StaffShiftAssignments.AsNoTracking()
            join s in _dbContext.Staffs on a.StaffId equals s.Id
            join d in _dbContext.Doctors on s.Id equals d.StaffId
            join u in _dbContext.AppUsers on s.AppUserId equals u.Id
            join r in _dbContext.Rooms on a.RoomId equals r.Id
            where a.RoomId == roomId && a.ShiftDate == date && a.StartsAtUtc <= atUtc && atUtc < a.EndsAtUtc
                && s.IsActive && s.StaffType == StaffType.Doctor && d.IsActive && u.IsActive && r.IsActive && r.RoomType != RoomType.Clinical
            select new { DoctorId = d.Id, StaffId = s.Id, s.FullName }).Distinct().OrderBy(x => x.DoctorId)
            .Select(x => new RoomReadingDoctorDto(x.DoctorId, x.StaffId, x.FullName)).ToListAsync(ct);
    }
}
