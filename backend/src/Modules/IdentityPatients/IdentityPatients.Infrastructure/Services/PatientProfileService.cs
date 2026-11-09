using System.Text.Json;
using IdentityPatients.Application.Common;
using IdentityPatients.Application.Patients;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed class PatientProfileService(
    IdentityPatientsDbContext dbContext,
    IIdentityPatientsModule identityModule) : IPatientProfileService
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;
    private readonly IIdentityPatientsModule _identityModule = identityModule;

    public async Task<ServiceResult<PatientProfileDto>> CreateProfileAsync(
        Guid userId,
        CreatePatientProfileRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return ServiceResult<PatientProfileDto>.Failure(400, "Dữ liệu không hợp lệ", "Họ và tên bệnh nhân không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return ServiceResult<PatientProfileDto>.Failure(400, "Dữ liệu không hợp lệ", "Số điện thoại không được để trống.");
        }

        if (!Enum.TryParse<Gender>(request.Gender, ignoreCase: true, out var gender))
        {
            return ServiceResult<PatientProfileDto>.Failure(400, "Dữ liệu không hợp lệ", "Giới tính không hợp lệ (Male, Female, Other).");
        }

        if (!Enum.TryParse<Relationship>(request.Relationship, ignoreCase: true, out var relationship))
        {
            return ServiceResult<PatientProfileDto>.Failure(400, "Dữ liệu không hợp lệ", "Mối quan hệ không hợp lệ (Self, Child, Parent, Spouse, Other).");
        }

        var normalizedNationalId = string.IsNullOrWhiteSpace(request.NationalId) ? null : request.NationalId.Trim();

        var now = DateTime.UtcNow;
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            FullName = request.FullName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = gender,
            NationalId = normalizedNationalId,
            PhoneNumber = request.PhoneNumber.Trim(),
            Address = request.Address.Trim(),
            CreatedAtUtc = now
        };

        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            PatientId = patient.Id,
            Relationship = relationship,
            VerificationStatus = VerificationStatus.Pending,
            IsActive = true,
            CreatedAtUtc = now
        };

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            _dbContext.Patients.Add(patient);
            _dbContext.PatientRepresentatives.Add(rep);
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            if (IsUniqueViolation(ex, "NationalId"))
            {
                return ServiceResult<PatientProfileDto>.Failure(
                    409,
                    "Trùng lặp dữ liệu",
                    "Số định danh công dân (CCCD) đã được đăng ký trong hệ thống. Vui lòng kiểm tra lại thông tin.");
            }

            if (IsUniqueViolation(ex, "Relationship") || IsUniqueViolation(ex, "AppUserId"))
            {
                return ServiceResult<PatientProfileDto>.Failure(
                    409,
                    "Hồ sơ đã tồn tại",
                    "Tài khoản của bạn đã có hồ sơ chính chủ (Self). Mỗi tài khoản chỉ có tối đa một hồ sơ chính chủ.");
            }

            return ServiceResult<PatientProfileDto>.Failure(
                409,
                "Xung đột dữ liệu",
                "Không thể tạo hồ sơ do xung đột dữ liệu.");
        }

        var dto = new PatientProfileDto(
            patient.Id,
            rep.Id,
            patient.FullName,
            patient.DateOfBirth,
            patient.Gender.ToString(),
            patient.NationalId,
            patient.PhoneNumber,
            patient.Address,
            rep.Relationship.ToString(),
            rep.VerificationStatus.ToString(),
            rep.IsActive);

        return ServiceResult<PatientProfileDto>.Success(dto, 201);
    }

    public async Task<ServiceResult<IReadOnlyList<PatientProfileDto>>> GetUserProfilesAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await (from r in _dbContext.PatientRepresentatives.AsNoTracking()
                          join p in _dbContext.Patients.AsNoTracking() on r.PatientId equals p.Id
                          where r.AppUserId == userId && r.IsActive
                          orderby r.CreatedAtUtc ascending
                          select new PatientProfileDto(
                              p.Id,
                              r.Id,
                              p.FullName,
                              p.DateOfBirth,
                              p.Gender.ToString(),
                              p.NationalId,
                              p.PhoneNumber,
                              p.Address,
                              r.Relationship.ToString(),
                              r.VerificationStatus.ToString(),
                              r.IsActive
                          )).ToListAsync(ct);

        return ServiceResult<IReadOnlyList<PatientProfileDto>>.Success(list);
    }

    public async Task<ServiceResult<PatientProfileDto>> GetProfileByIdAsync(Guid userId, Guid patientId, CancellationToken ct = default)
    {
        var canAccess = await _identityModule.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, ct);
        if (!canAccess)
        {
            return ServiceResult<PatientProfileDto>.Failure(403, "Truy cập bị từ chối", "Bạn không có quyền truy cập hồ sơ bệnh nhân này.");
        }

        var result = await (from r in _dbContext.PatientRepresentatives.AsNoTracking()
                            join p in _dbContext.Patients.AsNoTracking() on r.PatientId equals p.Id
                            where r.AppUserId == userId && r.PatientId == patientId && r.IsActive
                            select new PatientProfileDto(
                                p.Id,
                                r.Id,
                                p.FullName,
                                p.DateOfBirth,
                                p.Gender.ToString(),
                                p.NationalId,
                                p.PhoneNumber,
                                p.Address,
                                r.Relationship.ToString(),
                                r.VerificationStatus.ToString(),
                                r.IsActive
                            )).FirstOrDefaultAsync(ct);

        if (result is null)
        {
            return ServiceResult<PatientProfileDto>.Failure(404, "Không tìm thấy", "Không tìm thấy hồ sơ bệnh nhân.");
        }

        return ServiceResult<PatientProfileDto>.Success(result);
    }

    public async Task<ServiceResult<PatientProfileDto>> UpdateProfileAsync(
        Guid userId,
        Guid patientId,
        UpdatePatientProfileRequest request,
        CancellationToken ct = default)
    {
        var canAccess = await _identityModule.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, ct);
        if (!canAccess)
        {
            return ServiceResult<PatientProfileDto>.Failure(403, "Truy cập bị từ chối", "Bạn không có quyền cập nhật hồ sơ bệnh nhân này.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return ServiceResult<PatientProfileDto>.Failure(400, "Dữ liệu không hợp lệ", "Họ và tên bệnh nhân không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return ServiceResult<PatientProfileDto>.Failure(400, "Dữ liệu không hợp lệ", "Số điện thoại không được để trống.");
        }

        if (!Enum.TryParse<Gender>(request.Gender, ignoreCase: true, out var gender))
        {
            return ServiceResult<PatientProfileDto>.Failure(400, "Dữ liệu không hợp lệ", "Giới tính không hợp lệ (Male, Female, Other).");
        }

        var patient = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct);
        if (patient is null)
        {
            return ServiceResult<PatientProfileDto>.Failure(404, "Không tìm thấy", "Không tìm thấy hồ sơ bệnh nhân.");
        }

        var rep = await _dbContext.PatientRepresentatives
            .FirstOrDefaultAsync(r => r.AppUserId == userId && r.PatientId == patientId && r.IsActive, ct);
        if (rep is null)
        {
            return ServiceResult<PatientProfileDto>.Failure(403, "Truy cập bị từ chối", "Bạn không có quyền cập nhật hồ sơ này.");
        }

        patient.FullName = request.FullName.Trim();
        patient.DateOfBirth = request.DateOfBirth;
        patient.Gender = gender;
        patient.NationalId = string.IsNullOrWhiteSpace(request.NationalId) ? null : request.NationalId.Trim();
        patient.PhoneNumber = request.PhoneNumber.Trim();
        patient.Address = request.Address.Trim();

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (IsUniqueViolation(ex, "NationalId"))
            {
                return ServiceResult<PatientProfileDto>.Failure(
                    409,
                    "Trùng lặp dữ liệu",
                    "Số định danh công dân (CCCD) đã được đăng ký trong hệ thống. Vui lòng kiểm tra lại thông tin.");
            }

            return ServiceResult<PatientProfileDto>.Failure(409, "Xung đột dữ liệu", "Không thể cập nhật hồ sơ do xung đột dữ liệu.");
        }

        var dto = new PatientProfileDto(
            patient.Id,
            rep.Id,
            patient.FullName,
            patient.DateOfBirth,
            patient.Gender.ToString(),
            patient.NationalId,
            patient.PhoneNumber,
            patient.Address,
            rep.Relationship.ToString(),
            rep.VerificationStatus.ToString(),
            rep.IsActive);

        return ServiceResult<PatientProfileDto>.Success(dto);
    }

    public async Task<ServiceResult> UnlinkProfileAsync(Guid userId, Guid patientId, CancellationToken ct = default)
    {
        var rep = await _dbContext.PatientRepresentatives
            .FirstOrDefaultAsync(r => r.AppUserId == userId && r.PatientId == patientId && r.IsActive, ct);

        if (rep is null)
        {
            return ServiceResult.Failure(404, "Không tìm thấy", "Không tìm thấy liên kết hồ sơ cần ngưng liên kết.");
        }

        rep.IsActive = false;
        rep.RevokedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return ServiceResult.Success(204);
    }

    private static bool IsUniqueViolation(DbUpdateException ex, string keyPart)
    {
        if (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            return string.IsNullOrEmpty(keyPart) || sqlEx.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
        }

        return ex.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
    }
}
