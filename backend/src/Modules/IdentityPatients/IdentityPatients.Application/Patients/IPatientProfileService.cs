using IdentityPatients.Application.Common;
using IdentityPatients.Contracts;

namespace IdentityPatients.Application.Patients;

public sealed record CreatePatientProfileRequest(
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string? NationalId,
    string PhoneNumber,
    string Address,
    string Relationship);

public sealed record UpdatePatientProfileRequest(
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string? NationalId,
    string PhoneNumber,
    string Address);

public interface IPatientProfileService
{
    Task<ServiceResult<PatientProfileDto>> CreateProfileAsync(Guid userId, CreatePatientProfileRequest request, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<PatientProfileDto>>> GetUserProfilesAsync(Guid userId, CancellationToken ct = default);
    Task<ServiceResult<PatientProfileDto>> GetProfileByIdAsync(Guid userId, Guid patientId, CancellationToken ct = default);
    Task<ServiceResult<PatientProfileDto>> UpdateProfileAsync(Guid userId, Guid patientId, UpdatePatientProfileRequest request, CancellationToken ct = default);
    Task<ServiceResult> UnlinkProfileAsync(Guid userId, Guid patientId, CancellationToken ct = default);
}
