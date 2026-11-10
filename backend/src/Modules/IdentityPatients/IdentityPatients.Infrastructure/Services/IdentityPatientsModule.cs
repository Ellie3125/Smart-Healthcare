using IdentityPatients.Contracts;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed class IdentityPatientsModule(IdentityPatientsDbContext dbContext) : IIdentityPatientsModule
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;

    public async Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, CancellationToken ct = default)
    {
        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null || !user.IsActive)
        {
            return false;
        }

        // Nhân viên lâm sàng / tiếp tân có quyền truy cập hồ sơ chung (Clinical module sẽ kiểm thêm theo từng lượt khám)
        if (user.Role is Roles.Doctor or Roles.Nurse or Roles.Receptionist)
        {
            return true;
        }

        // Tài khoản phòng CLS và Admin kỹ thuật KHÔNG được phép đọc trực tiếp hồ sơ bệnh nhân (SEC-01)
        if (user.Role is Roles.RoomAccount or Roles.Admin)
        {
            return false;
        }

        // Bệnh nhân tự xem hồ sơ của mình
        if (user.Role == Roles.Patient)
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == patientId, ct);

            if (patient is null)
            {
                return false;
            }

            if (patient.OwnerUserId == userId)
            {
                return true;
            }

            // Kiểm tra người đại diện đã được xác thực (Verified)
            // TODO(tuần 2): Mở rộng đầy đủ quản lý đa hồ sơ qua PatientRepresentative (quan hệ cha/mẹ/con/giám hộ).
            var hasVerifiedRep = await _dbContext.PatientRepresentatives
                .AsNoTracking()
                .AnyAsync(r => r.AppUserId == userId
                               && r.PatientId == patientId
                               && r.IsActive
                               && r.VerificationStatus == VerificationStatus.Verified, ct);

            return hasVerifiedRep;
        }

        return false;
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
}

