using System.Text.Json;
using IdentityPatients.Application.Common;
using IdentityPatients.Application.Verifications;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Domain.StateMachines;
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
                PatientInputValidator.MaskNationalId(x.Patient.NationalId),
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

    public async Task<ServiceResult<VerificationDetailDto>> GetVerificationByIdAsync(
        Guid representativeId,
        CancellationToken ct = default)
    {
        var detail = await (from r in _dbContext.PatientRepresentatives.AsNoTracking()
                            join p in _dbContext.Patients.AsNoTracking() on r.PatientId equals p.Id
                            join u in _dbContext.AppUsers.AsNoTracking() on r.AppUserId equals u.Id
                            where r.Id == representativeId
                            select new VerificationDetailDto(
                                r.Id,
                                p.Id,
                                r.AppUserId,
                                p.FullName,
                                p.DateOfBirth,
                                p.Gender.ToString(),
                                PatientInputValidator.MaskNationalId(p.NationalId),
                                p.PhoneNumber,
                                p.Address,
                                r.Relationship.ToString(),
                                r.VerificationStatus.ToString(),
                                r.IsActive,
                                r.CreatedAtUtc,
                                r.RevokedAtUtc,
                                p.CreatedAtUtc,
                                u.UserName,
                                u.Email,
                                u.PhoneNumber
                            )).FirstOrDefaultAsync(ct);

        if (detail is null)
        {
            return ServiceResult<VerificationDetailDto>.Failure(404, "Không tìm thấy", "Không tìm thấy yêu cầu xác minh hồ sơ.");
        }

        return ServiceResult<VerificationDetailDto>.Success(detail);
    }

    public Task<ServiceResult<VerificationItemDto>> VerifyAsync(
        Guid staffUserId,
        Guid representativeId,
        CancellationToken ct = default) =>
        TransitionAsync(
            staffUserId,
            representativeId,
            VerificationStatus.Verified,
            reason: "Đối chiếu bản gốc và xác minh hợp lệ tại quầy tiếp tân",
            ct);

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

        return await TransitionAsync(
            staffUserId,
            representativeId,
            VerificationStatus.Rejected,
            reason: request.Reason.Trim(),
            ct);
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

        return await TransitionAsync(
            staffUserId,
            representativeId,
            VerificationStatus.Revoked,
            reason: request.Reason.Trim(),
            ct);
    }

    private async Task<ServiceResult<VerificationItemDto>> TransitionAsync(
        Guid staffUserId,
        Guid representativeId,
        VerificationStatus targetStatus,
        string reason,
        CancellationToken ct)
    {
        return await unitOfWork.ExecuteAsync(async _ =>
        {
            var rep = await LockRepresentativeAsync(representativeId, ct);
            if (rep is null)
            {
                return ServiceResult<VerificationItemDto>.Failure(404, "Không tìm thấy", "Không tìm thấy yêu cầu xác minh hồ sơ.");
            }

            var now = DateTime.UtcNow;
            var outcome = VerificationStateMachine.ApplyTransition(rep, targetStatus, now);
            if (!outcome.Succeeded)
            {
                var extensions = new Dictionary<string, object?>
                {
                    ["currentStatus"] = outcome.OldStatus.ToString(),
                    ["allowedTransitions"] = outcome.AllowedTransitions ?? []
                };

                return ServiceResult<VerificationItemDto>.Failure(
                    409,
                    "Chuyển trạng thái không hợp lệ",
                    outcome.ErrorDetail!,
                    extensions,
                    outcome.ErrorCode);
            }

            var patient = await _dbContext.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == rep.PatientId, ct);

            if (!outcome.IsIdempotent)
            {
                await _dbContext.SaveChangesAsync(ct);

                var changedColumns = outcome.OldIsActive != outcome.NewIsActive || rep.RevokedAtUtc is not null
                    ? "VerificationStatus,IsActive,RevokedAtUtc"
                    : "VerificationStatus";

                await auditLog.WriteAsync(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    PerformedByUserId = staffUserId,
                    TableName = "identity.PatientRepresentative",
                    RecordId = rep.Id,
                    ActionType = ActionType.UPDATE,
                    ChangedColumns = changedColumns,
                    OldValuesJson = JsonSerializer.Serialize(new { VerificationStatus = outcome.OldStatus.ToString(), IsActive = outcome.OldIsActive }),
                    NewValuesJson = JsonSerializer.Serialize(new { VerificationStatus = outcome.NewStatus.ToString(), IsActive = outcome.NewIsActive }),
                    ChangeReason = reason,
                    TimestampUtc = now
                }, ct);
            }

            return ServiceResult<VerificationItemDto>.Success(MapItemDto(rep, patient));
        }, ct);
    }

    private static VerificationItemDto MapItemDto(PatientRepresentative rep, Patient? patient) => new(
        rep.Id,
        rep.PatientId,
        rep.AppUserId,
        patient?.FullName ?? string.Empty,
        patient?.DateOfBirth ?? default,
        patient?.Gender.ToString() ?? string.Empty,
        PatientInputValidator.MaskNationalId(patient?.NationalId),
        patient?.PhoneNumber ?? string.Empty,
        patient?.Address ?? string.Empty,
        rep.Relationship.ToString(),
        rep.VerificationStatus.ToString(),
        rep.CreatedAtUtc,
        rep.RevokedAtUtc);
}
