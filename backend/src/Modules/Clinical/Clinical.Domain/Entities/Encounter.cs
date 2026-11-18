using Clinical.Domain.Enums;

namespace Clinical.Domain.Entities;

public sealed class Encounter
{
    public Guid Id { get; set; }
    // ID chéo module: scheduling.Appointment, không FK
    public Guid AppointmentId { get; set; }
    // ID chéo module: identity.Patient, không FK
    public Guid PatientId { get; set; }
    // ID chéo module: identity.Doctor, không FK
    public Guid OriginalDoctorId { get; set; }
    // ID chéo module: identity.Room, không FK
    public Guid OriginalRoomId { get; set; }
    public string SpecialtySnapshot { get; set; } = null!;
    public string DoctorRankSnapshot { get; set; } = null!;
    public DateTime CheckedInAtUtc { get; set; }
    public EncounterStatus Status { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
