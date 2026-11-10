namespace IdentityPatients.Contracts;

public sealed record DoctorDto(
    Guid DoctorId,
    string FullName,
    Guid SpecialtyId,
    string SpecialtyCode,
    Guid RankId,
    string RankName,
    bool IsActive);

public sealed record SpecialtyDto(
    Guid Id,
    string Code,
    string Name);

public sealed record DoctorRankDto(
    Guid Id,
    string Code,
    string Name,
    int SortOrder);

public sealed record RoomDto(
    Guid Id,
    string Code,
    string Name,
    string RoomType,
    bool IsActive);

public sealed record DoctorSearch(
    Guid? SpecialtyId = null,
    Guid? RankId = null,
    bool? IsActive = null);

public sealed record StaffDutyLeaderDto(
    Guid StaffId,
    string FullName,
    string StaffCode,
    Guid RoomId,
    DateOnly ShiftDate,
    string ShiftName);

public sealed record PatientProfileDto(
    Guid PatientId,
    Guid RepresentativeId,
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string? NationalId,
    string PhoneNumber,
    string Address,
    string Relationship,
    string VerificationStatus,
    bool IsActive);
