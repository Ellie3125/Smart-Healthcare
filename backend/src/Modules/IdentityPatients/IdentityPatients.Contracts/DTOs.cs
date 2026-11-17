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

/// <summary>Hồ sơ bệnh nhân tiếp tân tìm thấy; CCCD và SĐT đã che, chỉ để đối chiếu với giấy tờ.</summary>
/// <param name="IsVerified">Có ít nhất một người đại diện đang hiệu lực đã được xác minh.</param>
public sealed record ReceptionPatientDto(
    Guid PatientId,
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string MaskedPhoneNumber,
    string? MaskedNationalId,
    bool IsVerified);

/// <summary>Nhân viên đang hoạt động gắn với một tài khoản đăng nhập.</summary>
/// <param name="StaffType">Tên enum StaffType của Identity: Doctor, Nurse, Receptionist, Admin...</param>
public sealed record ActiveStaffDto(Guid StaffId, string StaffType, string FullName);

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
