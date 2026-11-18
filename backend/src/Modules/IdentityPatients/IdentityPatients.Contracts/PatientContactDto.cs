namespace IdentityPatients.Contracts;

public sealed record PatientContactDto(Guid PatientId, string FullName, string MaskedPhoneNumber);
