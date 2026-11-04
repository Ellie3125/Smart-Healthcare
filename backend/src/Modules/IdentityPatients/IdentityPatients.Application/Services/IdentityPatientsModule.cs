using IdentityPatients.Contracts;
using IdentityPatients.Contracts.Constants;
using IdentityPatients.Contracts.Dtos;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Application.Services;

public sealed class IdentityPatientsModule(IdentityPatientsDbContext dbContext) : IIdentityPatientsModule
{
    public async Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, CancellationToken ct = default)
    {
        // 1. Kiểm tra User có tồn tại và IsActive không
        var user = await dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null || !user.IsActive)
            return false;

        // 2. RoomAccount và Admin: luôn là false theo yêu cầu D (không có quyền truy cập chuyên môn)
        if (user.Role is Roles.RoomAccount or Roles.Admin)
            return false;

        // 3. Nhân viên y tế (Doctor, Nurse, Receptionist): true
        if (user.Role is Roles.Doctor or Roles.Nurse or Roles.Receptionist)
            return true;

        // 4. Bệnh nhân (Patient): kiểm tra chính chủ
        if (user.Role == Roles.Patient)
        {
            var isOwner = await dbContext.Patients
                .AsNoTracking()
                .AnyAsync(p => p.Id == patientId && p.OwnerUserId == userId, ct);

            if (isOwner)
                return true;

            // TODO(Tuần 2): Mở rộng cho PatientRepresentative với VerificationStatus = Verified
            // Kiểm tra quan hệ người đại diện hợp lệ:
            // return await dbContext.PatientRepresentatives
            //     .AsNoTracking()
            //     .AnyAsync(r => r.AppUserId == userId && r.PatientId == patientId && r.IsActive && r.VerificationStatus == VerificationStatus.Verified, ct);

            return false;
        }

        return false;
    }

    public async Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default)
    {
        return await dbContext.Doctors
            .AsNoTracking()
            .Include(d => d.Specialty)
            .Include(d => d.DoctorRank)
            .Where(d => d.Id == doctorId)
            .Select(d => new DoctorDto(
                d.Id,
                d.FullName,
                d.SpecialtyId,
                d.Specialty.Code,
                d.DoctorRankId,
                d.DoctorRank.Name,
                d.DefaultRoomId,
                d.IsActive
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default)
    {
        var q = dbContext.Doctors
            .AsNoTracking()
            .Include(d => d.Specialty)
            .Include(d => d.DoctorRank)
            .Where(d => d.IsActive);

        if (query.SpecialtyId.HasValue)
            q = q.Where(d => d.SpecialtyId == query.SpecialtyId.Value);

        if (query.RankId.HasValue)
            q = q.Where(d => d.DoctorRankId == query.RankId.Value);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
            q = q.Where(d => d.FullName.Contains(query.Keyword));

        return await q
            .Select(d => new DoctorDto(
                d.Id,
                d.FullName,
                d.SpecialtyId,
                d.Specialty.Code,
                d.DoctorRankId,
                d.DoctorRank.Name,
                d.DefaultRoomId,
                d.IsActive
            ))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default)
    {
        return await dbContext.Specialties
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new SpecialtyDto(s.Id, s.Code, s.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default)
    {
        return await dbContext.DoctorRanks
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .Select(r => new DoctorRankDto(r.Id, r.Code, r.Name, r.SortOrder))
            .ToListAsync(ct);
    }

    public async Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        return await dbContext.Rooms
            .AsNoTracking()
            .Where(r => r.Id == roomId)
            .Select(r => new RoomDto(r.Id, r.Code, r.Name, r.RoomType, r.IsActive))
            .FirstOrDefaultAsync(ct);
    }
}

