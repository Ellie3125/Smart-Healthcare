using System.Text.Json;
using IdentityPatients.Application.Common;
using IdentityPatients.Application.Verifications;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace IdentityPatients.Infrastructure.Services;

public sealed class VerificationService(
    IdentityPatientsDbContext dbContext,
    IUnitOfWork unitOfWork,
    AuditLogWriter auditLog) : IVerificationService
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;

    // Khóa hàng PatientRepresentative: hai tiếp tân thao tác cùng hồ sơ thì người sau thấy trạng thái mới.
    private async Task<PatientRepresentative?> LockRepresentativeAsync(Guid representativeId, CancellationToken ct)
    {
        var exists = await _dbContext.PatientRepresentatives
            .FromSql($"SELECT * FROM [identity].[PatientRepresentative] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {representativeId}")
            .AsNoTracking()
            .AnyAsync(ct);
        return exists ? await _dbContext.PatientRepresentatives.FindAsync([representativeId], ct) : null;
    }

    public async Task<ServiceResult<PagedResult<VerificationItemDto>>> SearchVerificationsAsync(
        VerificationSearchQuery query,
        CancellationToken ct = default)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : (query.PageSize > 50 ? 50 : query.PageSize);

        var baseQuery = from r in _dbContext.PatientRepresentatives.AsNoTracking()
                        join p in _dbContext.Patients.AsNoTracking() on r.PatientId equals p.Id
                        select new { Rep = r, Patient = p };

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<VerificationStatus>(query.Status, ignoreCase: true, out var status))
        {
            baseQuery = baseQuery.Where(x => x.Rep.VerificationStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            baseQuery = baseQuery.Where(x =>
                x.Patient.FullName.Contains(term) ||
                x.Patient.PhoneNumber.Contains(term) ||
                (x.Patient.NationalId != null && x.Patient.NationalId.Contains(term)));
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(x => x.Rep.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new VerificationItemDto(
                x.Rep.Id,
                x.Patient.Id,
                x.Rep.AppUserId,
                x.Patient.FullName,
                x.Patient.DateOfBirth,
                x.Patient.Gender.ToString(),
                MaskNationalId(x.Patient.NationalId),
                x.Patient.PhoneNumber,
                x.Patient.Address,
                x.Rep.Relationship.ToString(),
                x.Rep.VerificationStatus.ToString(),
                x.Rep.CreatedAtUtc,
                x.Rep.RevokedAtUtc
            ))
            .ToListAsync(ct);

        return ServiceResult<PagedResult<VerificationItemDto>>.Success(
            new PagedResult<VerificationItemDto>(items, totalCount, page, pageSize));
    }

    public async Task<ServiceResult<VerificationItemDto>> VerifyAsync(
        Guid staffUserId,
        Guid representativeId,
        CancellationToken ct = default)
    {
        return await unitOfWork.ExecuteAsync(async _ =>
        {
            var rep = await LockRepresentativeAsync(representativeId, ct);
            if (rep is null)
            {
                return ServiceResult<VerificationItemDto>.Failure(404, "Không tìm thấy", "Không tìm thấy yêu cầu xác minh hồ sơ.");
            }

            if (rep.VerificationStatus != VerificationStatus.Pending)
            {
                var extensions = new Dictionary<string, object?>
                {
                    ["currentStatus"] = rep.VerificationStatus.ToString(),
                    ["allowedTransitions"] = new[] { "Pending -> Verified", "Pending -> Rejected" }
                };

                return ServiceResult<VerificationItemDto>.Failure(
                    409,
                    "Chuyển trạng thái không hợp lệ",
                    $"Không thể duyệt xác minh hồ sơ đang ở trạng thái '{rep.VerificationStatus}'. Hồ sơ phải ở trạng thái 'Pending'.",
                    extensions);
            }

            var oldStatus = rep.VerificationStatus;
            rep.VerificationStatus = VerificationStatus.Verified;
            var now = DateTime.UtcNow;

            var patient = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == rep.PatientId, ct);

            await _dbContext.SaveChangesAsync(ct);

            await auditLog.WriteAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                PerformedByUserId = staffUserId,
                TableName = "identity.PatientRepresentative",
                RecordId = rep.Id,
                ActionType = ActionType.UPDATE,
                ChangedColumns = "VerificationStatus",
                OldValuesJson = JsonSerializer.Serialize(new { VerificationStatus = oldStatus.ToString() }),
                NewValuesJson = JsonSerializer.Serialize(new { VerificationStatus = rep.VerificationStatus.ToString() }),
                ChangeReason = "Đối chiếu bản gốc và xác minh hợp lệ tại quầy tiếp tân",
                TimestampUtc = now
            }, ct);


            var dto = new VerificationItemDto(
                rep.Id,
                rep.PatientId,
                rep.AppUserId,
                patient?.FullName ?? string.Empty,
                patient?.DateOfBirth ?? default,
                patient?.Gender.ToString() ?? string.Empty,
                MaskNationalId(patient?.NationalId),
                patient?.PhoneNumber ?? string.Empty,
                patient?.Address ?? string.Empty,
                rep.Relationship.ToString(),
                rep.VerificationStatus.ToString(),
                rep.CreatedAtUtc,
                rep.RevokedAtUtc);

            return ServiceResult<VerificationItemDto>.Success(dto);
        }, ct);
    }

    public async Task<ServiceResult<VerificationItemDto>> RejectAsync(
        Guid staffUserId,
        Guid representativeId,
        RejectVerificationRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<VerificationItemDto>.Failure(400, "Thiếu lý do từ chối", "Vui lòng nhập lý do từ chối xác minh.");
        }

        return await unitOfWork.ExecuteAsync(async _ =>
        {
            var rep = await LockRepresentativeAsync(representativeId, ct);
            if (rep is null)
            {
                return ServiceResult<VerificationItemDto>.Failure(404, "Không tìm thấy", "Không tìm thấy yêu cầu xác minh hồ sơ.");
            }

            if (rep.VerificationStatus != VerificationStatus.Pending)
            {
                var extensions = new Dictionary<string, object?>
                {
                    ["currentStatus"] = rep.VerificationStatus.ToString(),
                    ["allowedTransitions"] = new[] { "Pending -> Verified", "Pending -> Rejected" }
                };

                return ServiceResult<VerificationItemDto>.Failure(
                    409,
                    "Chuyển trạng thái không hợp lệ",
                    $"Không thể từ chối hồ sơ đang ở trạng thái '{rep.VerificationStatus}'. Hồ sơ phải ở trạng thái 'Pending'.",
                    extensions);
            }

            var oldStatus = rep.VerificationStatus;
            rep.VerificationStatus = VerificationStatus.Rejected;
            rep.IsActive = false;
            var now = DateTime.UtcNow;
            rep.RevokedAtUtc = now;

            var patient = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == rep.PatientId, ct);

            await _dbContext.SaveChangesAsync(ct);

            await auditLog.WriteAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                PerformedByUserId = staffUserId,
                TableName = "identity.PatientRepresentative",
                RecordId = rep.Id,
                ActionType = ActionType.UPDATE,
                ChangedColumns = "VerificationStatus,IsActive,RevokedAtUtc",
                OldValuesJson = JsonSerializer.Serialize(new { VerificationStatus = oldStatus.ToString(), IsActive = true }),
                NewValuesJson = JsonSerializer.Serialize(new { VerificationStatus = rep.VerificationStatus.ToString(), IsActive = false }),
                ChangeReason = request.Reason.Trim(),
                TimestampUtc = now
            }, ct);


            var dto = new VerificationItemDto(
                rep.Id,
                rep.PatientId,
                rep.AppUserId,
                patient?.FullName ?? string.Empty,
                patient?.DateOfBirth ?? default,
                patient?.Gender.ToString() ?? string.Empty,
                MaskNationalId(patient?.NationalId),
                patient?.PhoneNumber ?? string.Empty,
                patient?.Address ?? string.Empty,
                rep.Relationship.ToString(),
                rep.VerificationStatus.ToString(),
                rep.CreatedAtUtc,
                rep.RevokedAtUtc);

            return ServiceResult<VerificationItemDto>.Success(dto);
        }, ct);
    }

    public async Task<ServiceResult<VerificationItemDto>> RevokeAsync(
        Guid staffUserId,
        Guid representativeId,
        RevokeVerificationRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<VerificationItemDto>.Failure(400, "Thiếu lý do thu hồi", "Vui lòng nhập lý do thu hồi xác minh.");
        }

        return await unitOfWork.ExecuteAsync(async _ =>
        {
            var rep = await LockRepresentativeAsync(representativeId, ct);
            if (rep is null)
            {
                return ServiceResult<VerificationItemDto>.Failure(404, "Không tìm thấy", "Không tìm thấy yêu cầu xác minh hồ sơ.");
            }

            if (rep.VerificationStatus != VerificationStatus.Verified)
            {
                var extensions = new Dictionary<string, object?>
                {
                    ["currentStatus"] = rep.VerificationStatus.ToString(),
                    ["allowedTransitions"] = new[] { "Verified -> Revoked" }
                };

                return ServiceResult<VerificationItemDto>.Failure(
                    409,
                    "Chuyển trạng thái không hợp lệ",
                    $"Không thể thu hồi hồ sơ đang ở trạng thái '{rep.VerificationStatus}'. Hồ sơ phải ở trạng thái 'Verified'.",
                    extensions);
            }

            var oldStatus = rep.VerificationStatus;
            rep.VerificationStatus = VerificationStatus.Revoked;
            rep.IsActive = false;
            var now = DateTime.UtcNow;
            rep.RevokedAtUtc = now;

            var patient = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == rep.PatientId, ct);

            await _dbContext.SaveChangesAsync(ct);

            await auditLog.WriteAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                PerformedByUserId = staffUserId,
                TableName = "identity.PatientRepresentative",
                RecordId = rep.Id,
                ActionType = ActionType.UPDATE,
                ChangedColumns = "VerificationStatus,IsActive,RevokedAtUtc",
                OldValuesJson = JsonSerializer.Serialize(new { VerificationStatus = oldStatus.ToString(), IsActive = true }),
                NewValuesJson = JsonSerializer.Serialize(new { VerificationStatus = rep.VerificationStatus.ToString(), IsActive = false }),
                ChangeReason = request.Reason.Trim(),
                TimestampUtc = now
            }, ct);


            var dto = new VerificationItemDto(
                rep.Id,
                rep.PatientId,
                rep.AppUserId,
                patient?.FullName ?? string.Empty,
                patient?.DateOfBirth ?? default,
                patient?.Gender.ToString() ?? string.Empty,
                MaskNationalId(patient?.NationalId),
                patient?.PhoneNumber ?? string.Empty,
                patient?.Address ?? string.Empty,
                rep.Relationship.ToString(),
                rep.VerificationStatus.ToString(),
                rep.CreatedAtUtc,
                rep.RevokedAtUtc);

            return ServiceResult<VerificationItemDto>.Success(dto);
        }, ct);
    }

    private static string? MaskNationalId(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId))
        {
            return null;
        }

        var trimmed = nationalId.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        return new string('*', trimmed.Length - 4) + trimmed[^4..];
    }
}
