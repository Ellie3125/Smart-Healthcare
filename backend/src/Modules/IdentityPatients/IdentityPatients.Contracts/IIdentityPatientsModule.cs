namespace IdentityPatients.Contracts;

public enum AccessLevel
{
    Booking,
    ClinicalRecords
}

public interface IIdentityPatientsModule
{
    Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, AccessLevel level = AccessLevel.Booking, CancellationToken ct = default);
    Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default);
    Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default);
    Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default);
    Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default);
    Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid roomId, DateOnly date, string shiftName, CancellationToken ct = default);
}
