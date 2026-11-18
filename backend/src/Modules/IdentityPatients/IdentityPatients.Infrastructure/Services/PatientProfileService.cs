using System.Text.Json;
using IdentityPatients.Application.Common;
using IdentityPatients.Application.Patients;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Domain.StateMachines;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace IdentityPatients.Infrastructure.Services;

public sealed class PatientProfileService(
    IdentityPatientsDbContext dbContext,
    IIdentityPatientsModule identityModule,
    IUnitOfWork unitOfWork,
    AuditLogWriter auditLog) : IPatientProfileService
{
    public const string ProfileNotFoundCode = "ProfileNotFound";
    public const string SelfProfileExistsCode = "SelfProfileExists";
    public const string DuplicateNationalIdCode = "DuplicateNationalId";
    public const string VerifiedProfileIdentityLockedCode = "VerifiedProfileIdentityLocked";
    public const string MissingAuditReasonCode = "MissingAuditReason";

    // Hồ sơ không thuộc tài khoản trả cùng lỗi với hồ sơ không có, để không lộ sự tồn tại (D8).
    private static ServiceResult<PatientProfileDto> NotFound() =>
        ServiceResult<PatientProfileDto>.Failure(404, "Không tìm thấy", "Không tìm thấy hồ sơ bệnh nhân.", code: ProfileNotFoundCode);

    private readonly IdentityPatientsDbContext _dbContext = dbContext;
    private readonly IIdentityPatientsModule _identityModule = identityModule;

    public async Task<ServiceResult<PatientProfileDto>> CreateProfileAsync(
        Guid userId,
        CreatePatientProfileRequest request,
        CancellationToken ct = default)
    {
        var (fields, validationError) = PatientInputValidator.ValidatePatientFields(
            request.FullName,
            request.DateOfBirth,
            request.Gender,
            request.NationalId,
            request.PhoneNumber,
            request.Address);

        if (validationError is { } err)
        {
            return ServiceResult<PatientProfileDto>.Failure(400, err.Title, err.Detail, code: err.Code);
        }

        if (string.IsNullOrWhiteSpace(request.Relationship)
            || !Enum.TryParse<Relationship>(request.Relationship.Trim(), ignoreCase: true, out var relationship)
            || !Enum.IsDefined(relationship))
        {
            return ServiceResult<PatientProfileDto>.Failure(400, "Dữ liệu không hợp lệ", "Mối quan hệ không hợp lệ (Self, Child, Parent, Spouse, Other).");
        }

        var validated = fields!;
        if (validated.NationalId is not null)
        {
            var nationalIdExists = await _dbContext.Patients.AsNoTracking()
                .AnyAsync(p => p.NationalId == validated.NationalId, ct);
            if (nationalIdExists)
            {
                return DuplicateNationalIdFailure<PatientProfileDto>();
            }
        }

        var now = DateTime.UtcNow;
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = validated.FullName,
            DateOfBirth = validated.DateOfBirth,
            Gender = validated.Gender,
            NationalId = validated.NationalId,
            PhoneNumber = validated.PhoneNumber,
            Address = validated.Address,
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

        try
        {
            var created = await unitOfWork.ExecuteAsync(async token =>
            {
                if (relationship == Relationship.Self)
                {
                    // ERD không có unique cho "một hồ sơ Self đang hoạt động" (Backend kiểm): khóa hàng AppUser
                    // để hai yêu cầu đồng thời của cùng tài khoản không cùng vượt qua bước kiểm tra.
                    await _dbContext.AppUsers
                        .FromSql($"SELECT * FROM [identity].[AppUser] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}")
                        .AsNoTracking()
                        .SingleAsync(token);
                    if (await _dbContext.PatientRepresentatives.AnyAsync(
                            r => r.AppUserId == userId && r.Relationship == Relationship.Self && r.IsActive, token))
                    {
                        return false;
                    }
                }

                _dbContext.Patients.Add(patient);
                _dbContext.PatientRepresentatives.Add(rep);
                await _dbContext.SaveChangesAsync(token);
                return true;
            }, ct);
            if (!created)
            {
                return ServiceResult<PatientProfileDto>.Failure(
                    409,
                    "Hồ sơ đã tồn tại",
                    "Tài khoản của bạn đã có hồ sơ chính chủ (Self). Mỗi tài khoản chỉ có tối đa một hồ sơ chính chủ.",
                    code: SelfProfileExistsCode);
            }
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
            if (IsUniqueViolation(ex, "NationalId"))
            {
                return DuplicateNationalIdFailure<PatientProfileDto>();
            }

            return ServiceResult<PatientProfileDto>.Failure(
                409,
                "Xung đột dữ liệu",
                "Không thể tạo hồ sơ do xung đột dữ liệu.");
        }

        return ServiceResult<PatientProfileDto>.Success(MapDto(patient, rep), 201);
    }

    public async Task<ServiceResult<IReadOnlyList<PatientProfileDto>>> GetUserProfilesAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await (from r in _dbContext.PatientRepresentatives.AsNoTracking()
                          join p in _dbContext.Patients.AsNoTracking() on r.PatientId equals p.Id
                          where r.AppUserId == userId && (r.IsActive || r.VerificationStatus == VerificationStatus.Rejected)
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
            return NotFound();
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
            return NotFound();
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
            return NotFound();
        }

        var (fields, validationError) = PatientInputValidator.ValidatePatientFields(
            request.FullName,
            request.DateOfBirth,
            request.Gender,
            request.NationalId,
            request.PhoneNumber,
            request.Address);

        if (validationError is { } err)
        {
            return ServiceResult<PatientProfileDto>.Failure(400, err.Title, err.Detail, code: err.Code);
        }

        var patient = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct);
        if (patient is null)
        {
            return NotFound();
        }

        var rep = await _dbContext.PatientRepresentatives
            .FirstOrDefaultAsync(r => r.AppUserId == userId && r.PatientId == patientId && r.IsActive, ct);
        if (rep is null)
        {
            return NotFound();
        }

        var validated = fields!;

        if (rep.VerificationStatus == VerificationStatus.Verified)
        {
            var identityChanged =
                !string.Equals(patient.FullName, validated.FullName, StringComparison.Ordinal) ||
                patient.DateOfBirth != validated.DateOfBirth ||
                patient.Gender != validated.Gender ||
                !string.Equals(patient.NationalId, validated.NationalId, StringComparison.Ordinal);

            if (identityChanged)
            {
                return ServiceResult<PatientProfileDto>.Failure(
                    403,
                    "Không được phép sửa thông tin định danh",
                    "Hồ sơ đã được xác minh. Chỉ tiếp tân hoặc quản trị viên mới được sửa họ tên, ngày sinh, giới tính hoặc số CCCD.",
                    code: VerifiedProfileIdentityLockedCode);
            }
        }

        if (validated.NationalId is not null
            && !string.Equals(patient.NationalId, validated.NationalId, StringComparison.Ordinal)
            && await _dbContext.Patients.AsNoTracking().AnyAsync(p => p.Id != patientId && p.NationalId == validated.NationalId, ct))
        {
            return DuplicateNationalIdFailure<PatientProfileDto>();
        }

        if (rep.VerificationStatus != VerificationStatus.Verified)
        {
            patient.FullName = validated.FullName;
            patient.DateOfBirth = validated.DateOfBirth;
            patient.Gender = validated.Gender;
            patient.NationalId = validated.NationalId;
        }

        patient.PhoneNumber = validated.PhoneNumber;
        patient.Address = validated.Address;

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
            if (IsUniqueViolation(ex, "NationalId"))
            {
                return DuplicateNationalIdFailure<PatientProfileDto>();
            }

            return ServiceResult<PatientProfileDto>.Failure(409, "Xung đột dữ liệu", "Không thể cập nhật hồ sơ do xung đột dữ liệu.");
        }

        return ServiceResult<PatientProfileDto>.Success(MapDto(patient, rep));
    }

    public async Task<ServiceResult<PatientProfileDto>> ResubmitProfileAsync(
        Guid userId,
        Guid patientId,
        CancellationToken ct = default)
    {
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var repExists = await _dbContext.PatientRepresentatives
                .FromSql($"SELECT * FROM [identity].[PatientRepresentative] WITH (UPDLOCK, HOLDLOCK) WHERE [AppUserId] = {userId} AND [PatientId] = {patientId}")
                .AsNoTracking()
                .AnyAsync(token);

            if (!repExists)
            {
                return NotFound();
            }

            var rep = await _dbContext.PatientRepresentatives
                .FirstOrDefaultAsync(r => r.AppUserId == userId && r.PatientId == patientId, token);

            if (rep is null || (!rep.IsActive && rep.VerificationStatus != VerificationStatus.Rejected))
            {
                return NotFound();
            }

            var now = DateTime.UtcNow;
            var outcome = VerificationStateMachine.ApplyTransition(rep, VerificationStatus.Pending, now);
            if (!outcome.Succeeded)
            {
                var extensions = new Dictionary<string, object?>
                {
                    ["currentStatus"] = outcome.OldStatus.ToString(),
                    ["allowedTransitions"] = outcome.AllowedTransitions ?? []
                };

                return ServiceResult<PatientProfileDto>.Failure(
                    409,
                    "Chuyển trạng thái không hợp lệ",
                    outcome.ErrorDetail!,
                    extensions,
                    outcome.ErrorCode);
            }

            var patient = await _dbContext.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId, token);
            if (patient is null)
            {
                return NotFound();
            }

            if (!outcome.IsIdempotent)
            {
                await _dbContext.SaveChangesAsync(token);
                await auditLog.WriteAsync(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    PerformedByUserId = userId,
                    TableName = "identity.PatientRepresentative",
                    RecordId = rep.Id,
                    ActionType = ActionType.UPDATE,
                    ChangedColumns = "VerificationStatus,IsActive,RevokedAtUtc",
                    OldValuesJson = JsonSerializer.Serialize(new { VerificationStatus = outcome.OldStatus.ToString(), IsActive = outcome.OldIsActive }),
                    NewValuesJson = JsonSerializer.Serialize(new { VerificationStatus = outcome.NewStatus.ToString(), IsActive = outcome.NewIsActive }),
                    ChangeReason = "Người đại diện gửi lại yêu cầu xác minh hồ sơ",
                    TimestampUtc = now
                }, token);
            }

            return ServiceResult<PatientProfileDto>.Success(MapDto(patient, rep));
        }, ct);
    }

    public async Task<ServiceResult<StaffPatientDto>> UpdateProfileByStaffAsync(
        Guid staffUserId,
        Guid patientId,
        StaffUpdatePatientProfileRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<StaffPatientDto>.Failure(
                400,
                "Thiếu lý do chỉnh sửa",
                "Vui lòng nhập lý do khi chỉnh sửa hồ sơ bệnh nhân.",
                code: MissingAuditReasonCode);
        }

        var (fields, validationError) = PatientInputValidator.ValidatePatientFields(
            request.FullName,
            request.DateOfBirth,
            request.Gender,
            request.NationalId,
            request.PhoneNumber,
            request.Address);

        if (validationError is { } err)
        {
            return ServiceResult<StaffPatientDto>.Failure(400, err.Title, err.Detail, code: err.Code);
        }

        var validated = fields!;

        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var patient = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == patientId, token);
                if (patient is null)
                {
                    return ServiceResult<StaffPatientDto>.Failure(404, "Không tìm thấy", "Không tìm thấy hồ sơ bệnh nhân.", code: ProfileNotFoundCode);
                }

                var targetNationalId = request.KeepExistingNationalId && validated.NationalId is null
                    ? patient.NationalId
                    : validated.NationalId;

                if (targetNationalId is not null
                    && !string.Equals(patient.NationalId, targetNationalId, StringComparison.Ordinal)
                    && await _dbContext.Patients.AsNoTracking().AnyAsync(p => p.Id != patientId && p.NationalId == targetNationalId, token))
                {
                    return DuplicateNationalIdFailure<StaffPatientDto>();
                }

                var changedColumns = new List<string>();
                if (!string.Equals(patient.FullName, validated.FullName, StringComparison.Ordinal)) changedColumns.Add("FullName");
                if (patient.DateOfBirth != validated.DateOfBirth) changedColumns.Add("DateOfBirth");
                if (patient.Gender != validated.Gender) changedColumns.Add("Gender");
                if (!string.Equals(patient.NationalId, targetNationalId, StringComparison.Ordinal)) changedColumns.Add("NationalId");
                if (!string.Equals(patient.PhoneNumber, validated.PhoneNumber, StringComparison.Ordinal)) changedColumns.Add("PhoneNumber");
                if (!string.Equals(patient.Address, validated.Address, StringComparison.Ordinal)) changedColumns.Add("Address");

                if (changedColumns.Count > 0)
                {
                    var oldSnapshot = new
                    {
                        patient.FullName,
                        patient.DateOfBirth,
                        Gender = patient.Gender.ToString(),
                        NationalId = PatientInputValidator.MaskNationalId(patient.NationalId),
                        patient.PhoneNumber,
                        patient.Address
                    };

                    patient.FullName = validated.FullName;
                    patient.DateOfBirth = validated.DateOfBirth;
                    patient.Gender = validated.Gender;
                    patient.NationalId = targetNationalId;
                    patient.PhoneNumber = validated.PhoneNumber;
                    patient.Address = validated.Address;

                    var newSnapshot = new
                    {
                        patient.FullName,
                        patient.DateOfBirth,
                        Gender = patient.Gender.ToString(),
                        NationalId = PatientInputValidator.MaskNationalId(patient.NationalId),
                        patient.PhoneNumber,
                        patient.Address
                    };

                    await _dbContext.SaveChangesAsync(token);

                    await auditLog.WriteAsync(new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        PerformedByUserId = staffUserId,
                        TableName = "identity.Patient",
                        RecordId = patient.Id,
                        ActionType = ActionType.UPDATE,
                        ChangedColumns = string.Join(",", changedColumns),
                        OldValuesJson = JsonSerializer.Serialize(oldSnapshot),
                        NewValuesJson = JsonSerializer.Serialize(newSnapshot),
                        ChangeReason = request.Reason.Trim(),
                        TimestampUtc = DateTime.UtcNow
                    }, token);
                }

                return ServiceResult<StaffPatientDto>.Success(MapStaffDto(patient));
            }, ct);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
            if (IsUniqueViolation(ex, "NationalId"))
            {
                return DuplicateNationalIdFailure<StaffPatientDto>();
            }

            return ServiceResult<StaffPatientDto>.Failure(409, "Xung đột dữ liệu", "Không thể cập nhật hồ sơ do xung đột dữ liệu.");
        }
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

    private static ServiceResult<T> DuplicateNationalIdFailure<T>() =>
        ServiceResult<T>.Failure(
            409,
            "Trùng lặp dữ liệu",
            PatientInputValidator.DuplicateNationalIdMessage,
            code: DuplicateNationalIdCode);

    private static PatientProfileDto MapDto(Patient patient, PatientRepresentative rep) => new(
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

    private static StaffPatientDto MapStaffDto(Patient patient) => new(
        patient.Id,
        patient.FullName,
        patient.DateOfBirth,
        patient.Gender.ToString(),
        PatientInputValidator.MaskNationalId(patient.NationalId),
        patient.PhoneNumber,
        patient.Address,
        patient.CreatedAtUtc);

    private static bool IsUniqueViolation(DbUpdateException ex, string keyPart)
    {
        if (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            return string.IsNullOrEmpty(keyPart) || sqlEx.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
        }

        return ex.Message.Contains(keyPart, StringComparison.OrdinalIgnoreCase);
    }
}
