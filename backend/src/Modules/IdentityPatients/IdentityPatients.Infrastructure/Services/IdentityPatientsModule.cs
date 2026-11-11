using IdentityPatients.Contracts;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed class IdentityPatientsModule(IdentityPatientsDbContext dbContext) : IIdentityPatientsModule
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;

    public async Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, AccessLevel level = AccessLevel.Booking, CancellationToken ct = default)
    {
        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null || !user.IsActive)
        {
            return false;
        }

        // Nhân viên và RoomAccount không đi qua đường này để đọc dữ liệu bệnh nhân (FR04/SEC-01)
        if ((await AccountRoles.ResolveAsync(_dbContext, userId, ct))?.Role != Roles.Patient)
        {
            return false;
        }

        var rep = await _dbContext.PatientRepresentatives
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.AppUserId == userId && r.PatientId == patientId && r.IsActive, ct);

        if (rep is null)
        {
            return false;
        }

        return level switch
        {
            AccessLevel.Booking => rep.VerificationStatus is VerificationStatus.Pending or VerificationStatus.Verified,
            AccessLevel.ClinicalRecords => rep.VerificationStatus is VerificationStatus.Verified,
            _ => false
        };
    }

    public async Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default)
    {
        var query = from d in _dbContext.Doctors.AsNoTracking()
                    join s in _dbContext.Staffs.AsNoTracking() on d.StaffId equals s.Id
                    join spec in _dbContext.Specialties.AsNoTracking() on d.SpecialtyId equals spec.Id
                    join r in _dbContext.DoctorRanks.AsNoTracking() on d.DoctorRankId equals r.Id
                    where d.Id == doctorId && d.IsActive && s.IsActive && spec.IsActive && r.IsActive
                    select new DoctorDto(
                        d.Id,
                        s.FullName,
                        spec.Id,
                        spec.SpecialtyCode,
                        r.Id,
                        r.RankName,
                        true
                    );

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default)
    {
        if (query.IsActive == false)
        {
            return [];
        }

        var baseQuery = from d in _dbContext.Doctors.AsNoTracking()
                        join s in _dbContext.Staffs.AsNoTracking() on d.StaffId equals s.Id
                        join spec in _dbContext.Specialties.AsNoTracking() on d.SpecialtyId equals spec.Id
                        join r in _dbContext.DoctorRanks.AsNoTracking() on d.DoctorRankId equals r.Id
                        where d.IsActive && s.IsActive && spec.IsActive && r.IsActive
                        select new { Doctor = d, Staff = s, Specialty = spec, Rank = r };

        if (query.SpecialtyId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Specialty.Id == query.SpecialtyId.Value);
        }

        if (query.RankId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Rank.Id == query.RankId.Value);
        }

        return await baseQuery
            .Select(x => new DoctorDto(
                x.Doctor.Id,
                x.Staff.FullName,
                x.Specialty.Id,
                x.Specialty.SpecialtyCode,
                x.Rank.Id,
                x.Rank.RankName,
                true
            ))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default)
    {
        return await _dbContext.Specialties
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SpecialtyName)
            .Select(s => new SpecialtyDto(s.Id, s.SpecialtyCode, s.SpecialtyName))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default)
    {
        return await _dbContext.DoctorRanks
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.DisplayOrder)
            .Select(r => new DoctorRankDto(r.Id, r.RankCode.ToString(), r.RankName, r.DisplayOrder))
            .ToListAsync(ct);
    }

    public async Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _dbContext.Rooms
            .AsNoTracking()
            .Where(r => r.Id == roomId && r.IsActive)
            .Select(r => new RoomDto(
                r.Id,
                r.RoomNumber,
                r.RoomName,
                r.RoomType.ToString(),
                true
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default)
    {
        if (isActive == false)
        {
            return [];
        }

        var query = _dbContext.Rooms.AsNoTracking().Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(roomType) && Enum.TryParse<RoomType>(roomType, ignoreCase: true, out var parsedType) && Enum.IsDefined(parsedType))
        {
            query = query.Where(r => r.RoomType == parsedType);
        }

        return await query
            .OrderBy(r => r.RoomNumber)
            .Select(r => new RoomDto(r.Id, r.RoomNumber, r.RoomName, r.RoomType.ToString(), true))
            .ToListAsync(ct);
    }

    public async Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid roomId, DateOnly date, string shiftName, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ShiftName>(shiftName, ignoreCase: true, out var parsedShift) || !Enum.IsDefined(parsedShift))
        {
            return null;
        }

        var query = from a in _dbContext.StaffShiftAssignments.AsNoTracking()
                    join s in _dbContext.Staffs.AsNoTracking() on a.StaffId equals s.Id
                    join r in _dbContext.Rooms.AsNoTracking() on a.RoomId equals r.Id
                    where a.RoomId == roomId
                          && a.ShiftDate == date
                          && a.ShiftName == parsedShift
                          && a.IsShiftLeader
                          && s.IsActive
                          && r.IsActive
                    select new StaffDutyLeaderDto(
                        s.Id,
                        s.FullName,
                        s.StaffCode,
                        a.RoomId,
                        a.ShiftDate,
                        a.ShiftName.ToString()
                    );

        return await query.FirstOrDefaultAsync(ct);
    }
}
