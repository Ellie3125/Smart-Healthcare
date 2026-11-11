using Scheduling.Domain.Enums;

namespace Scheduling.Application.Appointments;

/// <summary>Lịch hẹn mà AppUser đã tự đặt (Reservation.AppUserId). Quyền trên hồ sơ do endpoint kiểm lại qua Identity.</summary>
public interface IPatientAppointmentReader
{
    // shortcut: chỉ lịch do chính người gọi đặt; lịch tiếp tân đặt hộ tại quầy chưa hiện, bổ sung khi Identity có API liệt kê hồ sơ được đại diện.
    Task<IReadOnlyList<PatientAppointment>> ListAsync(Guid appUserId, int offset, int limit, CancellationToken ct);

    Task<PatientAppointment?> GetAsync(Guid appointmentId, Guid appUserId, CancellationToken ct);
}

public sealed record PatientAppointment(Guid AppointmentId, string AppointmentNumber, AppointmentStatus Status, Guid PatientId,
    Guid DoctorId, DateOnly ServiceDate, Guid AssignedRoomId, decimal AppliedConsultationFee, DateTime ConfirmedAtUtc,
    DateTime? CheckInDeadlineUtc, DateTime? CancelledAtUtc);
