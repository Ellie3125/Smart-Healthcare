namespace IdentityPatients.Contracts;

public interface IIdentityPatientsModule
{
    Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, CancellationToken ct = default);
    Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default);
    Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default);
    Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default);
}

