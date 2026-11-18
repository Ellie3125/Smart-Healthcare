using Scheduling.Domain.Enums;

namespace Scheduling.Application.Appointments;

/// <summary>Lịch hẹn của mọi hồ sơ AppUser có quyền Booking; endpoint kiểm lại quyền trước trả dữ liệu.</summary>
public interface IPatientAppointmentReader
{
    Task<IReadOnlyList<PatientAppointment>> ListAsync(Guid appUserId, int offset, int limit, CancellationToken ct);

    Task<PatientAppointment?> GetAsync(Guid appointmentId, Guid appUserId, CancellationToken ct);
}

public sealed record PatientAppointment(Guid AppointmentId, string AppointmentNumber, AppointmentStatus Status, Guid PatientId,
    Guid DoctorId, DateOnly ServiceDate, Guid AssignedRoomId, decimal AppliedConsultationFee, DateTime ConfirmedAtUtc,
    DateTime? CheckInDeadlineUtc, DateTime? CancelledAtUtc, DateTime? RescheduleCutoffTimeUtc = null,
    BookingChannel BookingChannel = BookingChannel.Web, bool IsBookedByCaller = true);
