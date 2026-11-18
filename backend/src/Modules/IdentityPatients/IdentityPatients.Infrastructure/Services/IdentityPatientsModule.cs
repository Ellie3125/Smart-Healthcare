using IdentityPatients.Contracts;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed partial class IdentityPatientsModule(IdentityPatientsDbContext dbContext) : IIdentityPatientsModule
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;

    public async Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, AccessLevel level = AccessLevel.Booking, CancellationToken ct = default) =>
        (await GetAccessiblePatientsAsync(userId, [patientId], level, ct)).Contains(patientId);

    /// <summary>
    /// Một truy vấn (trước đây 4 truy vấn mỗi hồ sơ). Luật giữ nguyên: AppUser đang hoạt động, vai trò Patient (không có
    /// RoomAccount, không có Staff — xem <see cref="AccountRoles"/>), đại diện đang hiệu lực; Booking nhận Pending/Verified,
    /// ClinicalRecords chỉ Verified. Nhân viên và RoomAccount không đi đường này (FR04/SEC-01).
    /// </summary>
    public async Task<IReadOnlySet<Guid>> GetAccessiblePatientsAsync(Guid userId, IReadOnlyCollection<Guid> patientIds,
        AccessLevel level = AccessLevel.Booking, CancellationToken ct = default)
    {
        var ids = patientIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<Guid>();
        }

        return (await Accessible(userId, level).Where(id => ids.Contains(id)).ToListAsync(ct)).ToHashSet();
    }

    public async Task<IReadOnlyList<Guid>> ListAccessiblePatientsAsync(Guid userId, AccessLevel level = AccessLevel.Booking,
        CancellationToken ct = default) => await Accessible(userId, level).Distinct().ToListAsync(ct);

    // Luật chung của hai hàm trên: AppUser hoạt động, chỉ vai trò Patient, đại diện còn hiệu lực đúng mức xác minh.
    private IQueryable<Guid> Accessible(Guid userId, AccessLevel level)
    {
        VerificationStatus[] statuses = level switch
        {
            AccessLevel.Booking => [VerificationStatus.Pending, VerificationStatus.Verified],
            AccessLevel.ClinicalRecords => [VerificationStatus.Verified],
            _ => []
        };
        return _dbContext.PatientRepresentatives.AsNoTracking()
            .Where(r => r.AppUserId == userId && r.IsActive && statuses.Contains(r.VerificationStatus)
                && _dbContext.AppUsers.Any(u => u.Id == userId && u.IsActive)
                && !_dbContext.RoomAccounts.Any(a => a.AppUserId == userId)
                && !_dbContext.Staffs.Any(s => s.AppUserId == userId))
            .Select(r => r.PatientId);
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

    public async Task<IReadOnlyList<ReceptionPatientDto>> FindPatientsForReceptionAsync(string keyword, CancellationToken ct = default)
    {
        var key = string.Concat((keyword ?? "").Where(c => !char.IsWhiteSpace(c)));
        if (key.Length is < 9 or > 20)
        {
            return [];
        }

        // Khớp chính xác: index IX_Patient_PhoneNumber và index unique có lọc của NationalId; không tìm gần đúng để tránh lộ hồ sơ.
        var rows = await _dbContext.Patients.AsNoTracking()
            .Where(p => p.PhoneNumber == key || p.NationalId == key)
            .OrderBy(p => p.FullName).ThenBy(p => p.Id)
            .Take(20)
            .Select(p => new
            {
                p.Id, p.FullName, p.DateOfBirth, p.Gender, p.PhoneNumber, p.NationalId,
                IsVerified = _dbContext.PatientRepresentatives.Any(r => r.PatientId == p.Id && r.IsActive
                    && r.VerificationStatus == VerificationStatus.Verified)
            })
            .ToListAsync(ct);
        return rows.Select(p => new ReceptionPatientDto(p.Id, p.FullName, p.DateOfBirth, p.Gender.ToString(),
            Mask(p.PhoneNumber)!, Mask(p.NationalId), p.IsVerified)).ToList();
    }

    public Task<bool> PatientExistsAsync(Guid patientId, CancellationToken ct = default) =>
        _dbContext.Patients.AsNoTracking().AnyAsync(p => p.Id == patientId, ct);

    public Task<ActiveStaffDto?> GetActiveStaffAsync(Guid appUserId, CancellationToken ct = default) =>
        _dbContext.Staffs.AsNoTracking()
            .Where(s => s.AppUserId == appUserId && s.IsActive && _dbContext.AppUsers.Any(u => u.Id == appUserId && u.IsActive))
            .Select(s => new ActiveStaffDto(s.Id, s.StaffType.ToString(), s.FullName))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetDoctorShiftRoomIdsAsync(Guid doctorId, DateOnly date, CancellationToken ct = default) =>
        await (from d in _dbContext.Doctors.AsNoTracking()
               join a in _dbContext.StaffShiftAssignments.AsNoTracking() on d.StaffId equals a.StaffId
               where d.Id == doctorId && a.ShiftDate == date
               select a.RoomId).Distinct().ToListAsync(ct);

    /// <summary>Giữ 3 ký tự cuối để tiếp tân đối chiếu với giấy tờ.</summary>
    private static string? Mask(string? value) =>
        value is null ? null : value.Length <= 3 ? new string('*', value.Length) : new string('*', value.Length - 3) + value[^3..];
}
