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
        if (user.Role != Roles.Patient)
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
                    where d.Id == doctorId
                    select new DoctorDto(
                        d.Id,
                        s.FullName,
                        spec.Id,
                        spec.SpecialtyCode,
                        r.Id,
                        r.RankName,
                        d.DefaultRoomId,
                        d.IsActive && s.IsActive
                    );

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default)
    {
        var baseQuery = from d in _dbContext.Doctors.AsNoTracking()
                        join s in _dbContext.Staffs.AsNoTracking() on d.StaffId equals s.Id
                        join spec in _dbContext.Specialties.AsNoTracking() on d.SpecialtyId equals spec.Id
                        join r in _dbContext.DoctorRanks.AsNoTracking() on d.DoctorRankId equals r.Id
                        select new { Doctor = d, Staff = s, Specialty = spec, Rank = r };

        if (query.SpecialtyId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Specialty.Id == query.SpecialtyId.Value);
        }

        if (query.RankId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Rank.Id == query.RankId.Value);
        }

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Doctor.IsActive == query.IsActive.Value && x.Staff.IsActive == query.IsActive.Value);
        }

        var results = await baseQuery
            .Select(x => new DoctorDto(
                x.Doctor.Id,
                x.Staff.FullName,
                x.Specialty.Id,
                x.Specialty.SpecialtyCode,
                x.Rank.Id,
                x.Rank.RankName,
                x.Doctor.DefaultRoomId,
                x.Doctor.IsActive && x.Staff.IsActive
            ))
            .ToListAsync(ct);

        return results;
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
            .Where(r => r.Id == roomId)
            .Select(r => new RoomDto(
                r.Id,
                r.RoomNumber,
                r.RoomName,
                r.RoomType.ToString(),
                r.IsActive
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _dbContext.Rooms.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(roomType) && Enum.TryParse<RoomType>(roomType, ignoreCase: true, out var parsedType))
        {
            query = query.Where(r => r.RoomType == parsedType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(r => r.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(r => r.RoomNumber)
            .Select(r => new RoomDto(r.Id, r.RoomNumber, r.RoomName, r.RoomType.ToString(), r.IsActive))
            .ToListAsync(ct);
    }

    public async Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid roomId, DateOnly date, string shiftName, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ShiftName>(shiftName, ignoreCase: true, out var parsedShift))
        {
            return null;
        }

        var query = from a in _dbContext.StaffShiftAssignments.AsNoTracking()
                    join s in _dbContext.Staffs.AsNoTracking() on a.StaffId equals s.Id
                    where a.RoomId == roomId && a.ShiftDate == date && a.ShiftName == parsedShift && a.IsShiftLeader
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
