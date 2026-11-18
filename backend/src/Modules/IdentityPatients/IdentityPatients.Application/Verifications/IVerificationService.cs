using IdentityPatients.Application.Common;

namespace IdentityPatients.Application.Verifications;

public sealed record VerificationSearchQuery(
    string? SearchTerm = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20);

public sealed record VerificationItemDto(
    Guid RepresentativeId,
    Guid PatientId,
    Guid AppUserId,
    string PatientFullName,
    DateOnly DateOfBirth,
    string Gender,
    string? MaskedNationalId,
    string PhoneNumber,
    string Address,
    string Relationship,
    string VerificationStatus,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc);

public sealed record VerificationDetailDto(
    Guid RepresentativeId,
    Guid PatientId,
    Guid AppUserId,
    string PatientFullName,
    DateOnly DateOfBirth,
    string Gender,
    string? MaskedNationalId,
    string PhoneNumber,
    string Address,
    string Relationship,
    string VerificationStatus,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc,
    DateTime PatientCreatedAtUtc,
    string RepresentativeUserName,
    string? RepresentativeEmail,
    string? RepresentativePhoneNumber);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record RejectVerificationRequest(string Reason);

public sealed record RevokeVerificationRequest(string Reason);

public interface IVerificationService
{
    Task<ServiceResult<PagedResult<VerificationItemDto>>> SearchVerificationsAsync(VerificationSearchQuery query, CancellationToken ct = default);
    Task<ServiceResult<VerificationDetailDto>> GetVerificationByIdAsync(Guid representativeId, CancellationToken ct = default);
    Task<ServiceResult<VerificationItemDto>> VerifyAsync(Guid staffUserId, Guid representativeId, CancellationToken ct = default);
    Task<ServiceResult<VerificationItemDto>> RejectAsync(Guid staffUserId, Guid representativeId, RejectVerificationRequest request, CancellationToken ct = default);
    Task<ServiceResult<VerificationItemDto>> RevokeAsync(Guid staffUserId, Guid representativeId, RevokeVerificationRequest request, CancellationToken ct = default);
}
