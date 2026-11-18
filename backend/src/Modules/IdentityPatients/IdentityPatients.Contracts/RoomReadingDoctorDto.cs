namespace IdentityPatients.Contracts;

public sealed record RoomReadingDoctorDto(Guid DoctorId, Guid StaffId, string FullName);
