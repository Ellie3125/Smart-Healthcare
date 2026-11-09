using Shared.Abstractions;

namespace Scheduling.Contracts;

/// <summary>
/// Scheduling phát khi xác nhận lịch từ giữ chỗ hợp lệ sau PaymentVerified (W2-06), hoặc khi đổi lịch tạo lịch mới (W2-07),
/// cùng transaction với Appointment. Bên nhận: Notifications (báo web bệnh nhân qua SignalR). Phát lặp được; chống lặp bằng ProcessedEvent.
/// </summary>
/// <param name="AppointmentNumber">Mã lịch hẹn, không phải số thứ tự khám.</param>
/// <param name="RescheduledFromAppointmentId">Có khi lịch này sinh ra do đổi lịch.</param>
public sealed record AppointmentConfirmed(
    Guid EventId,
    Guid AppointmentId,
    Guid BookingAttemptId,
    Guid PatientId,
    Guid DoctorId,
    DateOnly ServiceDate,
    string AppointmentNumber,
    Guid? RescheduledFromAppointmentId,
    DateTime OccurredAtUtc,
    Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;

    public int Version => CurrentVersion;
}

/// <summary>Ai hủy lịch.</summary>
public enum CancellationInitiator
{
    /// <summary>Bệnh nhân hoặc người đại diện tự hủy trên web bệnh nhân/chatbot.</summary>
    Patient,

    /// <summary>Tiếp tân hủy theo yêu cầu của bệnh nhân (cùng chính sách hoàn như bệnh nhân tự hủy).</summary>
    Receptionist,

    /// <summary>Bệnh viện hủy: không có bác sĩ thay, bệnh nhân không đồng ý bác sĩ thay (Phạm vi §6.3).</summary>
    Hospital,
}

/// <summary>Quyền hoàn phí khám ban đầu do Scheduling quyết định theo chính sách; Billing không tự tính lại mốc 05:00.</summary>
public enum ConsultationRefund
{
    /// <summary>Hoàn toàn bộ phí khám ban đầu đã trả: hủy trước 05:00 ngày D, hoặc bệnh viện hủy.</summary>
    Full,

    /// <summary>Không hoàn: bệnh nhân/tiếp tân hủy từ 05:00 ngày D (Phạm vi §6.2).</summary>
    None,
}

/// <summary>
/// Scheduling phát khi hủy lịch có hiệu lực (W2-07, W3-12), cùng transaction với trạng thái Cancelled và bộ đếm suất.
/// Bên nhận: Billing (tạo yêu cầu hoàn khi <see cref="Refund"/> = Full và đã thu tiền; chưa thu thì không làm gì),
/// Notifications. Đổi lịch KHÔNG phát sự kiện này (lịch cũ chuyển Rescheduled, phí chuyển sang lịch mới).
/// </summary>
/// <param name="BookingAttemptId">Billing tìm khoản phí khám đã thu theo BookingAttemptId (PaymentVerified.BookingAttemptId).</param>
/// <param name="CancelledAtUtc">Giờ DB lúc hủy có hiệu lực; căn cứ mốc 05:00.</param>
/// <param name="RescheduleCutoffTimeUtc">05:00 ngày D (UTC) của phiên, để Billing và đối soát kiểm lại.</param>
/// <param name="SlotReleased">true nếu trả suất (AdmittedCount − 1), false nếu khóa suất (LockedCancelledCount). Bệnh nhân/tiếp tân hủy: true trước 05:00;
/// bệnh viện hủy: true ở mọi giờ (đề xuất Q3, chờ trưởng nhóm duyệt).</param>
public sealed record AppointmentCancelled(
    Guid EventId,
    Guid AppointmentId,
    Guid BookingAttemptId,
    Guid PatientId,
    Guid DoctorId,
    DateOnly ServiceDate,
    CancellationInitiator CancelledBy,
    ConsultationRefund Refund,
    bool SlotReleased,
    DateTime CancelledAtUtc,
    DateTime RescheduleCutoffTimeUtc,
    DateTime OccurredAtUtc,
    Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;

    public int Version => CurrentVersion;
}

/// <summary>
/// Scheduling phát khi check in lần đầu (W3-02), cùng transaction với Appointment CheckedIn.
/// Bên nhận: Clinical mở Encounter theo AppointmentId (Encounter.AppointmentId unique) rồi phát EncounterOpened.
/// </summary>
/// <param name="AssignedRoomId">Phòng khám của phiên; Clinical lưu làm OriginalRoomId.</param>
/// <param name="QueueTicketId">Vé khám đầu. PENDING câu hỏi mở Encounter (docs/api/scheduling.md §6): null nếu vé cấp sau khi có Encounter.</param>
public sealed record TicketCheckedIn(
    Guid EventId,
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid AssignedRoomId,
    DateOnly ServiceDate,
    DateTime CheckedInAtUtc,
    Guid? QueueTicketId,
    DateTime OccurredAtUtc,
    Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;

    public int Version => CurrentVersion;
}
