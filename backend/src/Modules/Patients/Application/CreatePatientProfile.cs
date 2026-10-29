using SmartHealthcare.Patients.Domain;
using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Patients.Application;

public sealed record CreatePatientProfileCommand(
    Guid AppUserId,
    string? Relationship,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? NationalId,
    string? PhoneNumber,
    string? Address);

/// <summary>Tài khoản tạo hồ sơ cho chính mình (Self, tối đa một) hoặc cho người mình đại diện.</summary>
public sealed class CreatePatientProfile(IPatientProfileRepository profiles, TimeProvider clock)
{
    private static readonly Error SelfProfileExists =
        Error.Conflict("self_profile_exists", "Tài khoản đã có hồ sơ của chính mình.");

    public async Task<Result<PatientProfileDto>> ExecuteAsync(CreatePatientProfileCommand command, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!DomainEnum.TryParse<Relationship>(command.Relationship, out var relationship))
            errors["relationship"] = ["Quan hệ là Self, Child, Parent, Spouse hoặc Other."];

        var details = PatientDetails.Create(
            command.FullName, command.DateOfBirth, command.Gender, command.NationalId, command.PhoneNumber, command.Address,
            VietnamTime.Today(clock));
        if (!details.IsSuccess)
            foreach (var (field, messages) in details.Error.Fields!) errors[field] = messages;
        if (errors.Count > 0) return Error.Validation(errors);

        if (relationship == Relationship.Self && await profiles.HasActiveSelfLinkAsync(command.AppUserId, ct))
            return SelfProfileExists;

        var now = clock.GetUtcDateTime();
        var patient = Patient.Create(details.Value!, now);
        var link = PatientRepresentative.ForProfileCreator(command.AppUserId, patient.Id, relationship, now);

        return await profiles.TryAddAsync(patient, link, ct) switch
        {
            AddProfileOutcome.Added => PatientProfileDto.From(patient, link),
            AddProfileOutcome.SelfProfileExists => SelfProfileExists,
            _ => ProfileErrors.DuplicateNationalId,
        };
    }
}
