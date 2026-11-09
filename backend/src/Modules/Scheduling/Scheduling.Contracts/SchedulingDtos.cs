namespace Scheduling.Contracts;

// ---------- Tra suất trống ----------

/// <param name="DoctorIds">1..<see cref="MaxDoctors"/> bác sĩ, không trùng, không Guid rỗng.</param>
/// <param name="ServiceDate">Ngày phục vụ D theo giờ Việt Nam.</param>
public sealed record GetDoctorAvailability(IReadOnlyList<Guid> DoctorIds, DateOnly ServiceDate)
{
    /// <summary>Bằng giới hạn pageSize của API tìm bác sĩ, để một trang kết quả tra trong một lần gọi.</summary>
    public const int MaxDoctors = 50;
}

public enum AvailabilityStatus
{
    /// <summary>Lịch mở, còn suất, chưa tới BookingCutoff: nhận giữ chỗ.</summary>
    Open,

    /// <summary>Hết suất (AvailableSlots = 0).</summary>
    Full,

    /// <summary>Giờ DB đã tới BookingCutoffTimeUtc (09:30 ngày D).</summary>
    CutoffPassed,

    /// <summary>Phiên có nhưng IsOpen = false (admin đóng ngày) hoặc phiên bị hủy.</summary>
    Closed,

    /// <summary>Bác sĩ chưa có phiên ngày đó.</summary>
    NotScheduled,
}

/// <summary>
/// Suất trống của một bác sĩ trong ngày D. Khi <see cref="Status"/> = NotScheduled thì các trường của phiên là null và AvailableSlots = 0.
/// Thứ tự ưu tiên khi nhiều điều kiện cùng đúng: NotScheduled, Closed, CutoffPassed, Full, Open.
/// </summary>
/// <param name="AvailableSlots">max(0, Capacity − AdmittedCount − HeldCount − LockedCancelledCount); 0 khi Status khác Open.</param>
/// <param name="AssignedRoomId">Phòng khám của phiên (identity.Room).</param>
public sealed record DoctorDayAvailability(
    Guid DoctorId,
    DateOnly ServiceDate,
    AvailabilityStatus Status,
    int AvailableSlots,
    Guid? DoctorDayQuotaId,
    int? Capacity,
    Guid? AssignedRoomId,
    DateTime? SessionStartTimeUtc,
    DateTime? BookingCutoffTimeUtc);

// ---------- Check in ----------

/// <param name="PerformedByUserId">Tài khoản tiếp tân thực hiện (identity.AppUser), ghi AuditLog.</param>
/// <param name="OperationId">workflow.Operation của lệnh, nếu có; đi kèm sự kiện TicketCheckedIn.</param>
public sealed record CheckInAppointment(Guid AppointmentId, Guid PerformedByUserId, Guid? OperationId);

/// <summary>Kết quả check in.</summary>
/// <param name="CheckedInAtUtc">Giờ DB lúc check in có hiệu lực (lần đầu).</param>
/// <param name="AlreadyCheckedIn">true khi lịch đã check in từ trước; các trường trả lại giá trị của lần đầu.</param>
/// <param name="QueueTicketId">Vé khám đầu. PENDING câu hỏi mở Encounter (docs/api/scheduling.md §6): null nếu vé được cấp sau khi Clinical mở Encounter.</param>
/// <param name="TicketNumber">Số vé hiển thị; null cùng điều kiện với QueueTicketId.</param>
/// <param name="SequenceNumber">Thứ tự trong hàng chờ khám đầu của bác sĩ, tăng theo thời điểm check in; null cùng điều kiện.</param>
public sealed record CheckInResult(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    DateOnly ServiceDate,
    DateTime CheckedInAtUtc,
    bool AlreadyCheckedIn,
    Guid? QueueTicketId,
    string? TicketNumber,
    int? SequenceNumber);

// ---------- Vé CLS và vé quay lại ----------

/// <param name="EncounterId">Lượt khám (clinical.Encounter) đã có vé khám đầu.</param>
/// <param name="ServiceOrderId">Đợt chỉ định (clinical.ServiceOrder); khóa idempotency của vé quay lại.</param>
/// <param name="Lines">Không rỗng, ServiceOrderItemId không trùng.</param>
/// <param name="OperationId">workflow.Operation của lệnh chỉ định, nếu có.</param>
public sealed record CreateClsTickets(Guid EncounterId, Guid ServiceOrderId, IReadOnlyList<ClsTicketLine> Lines, Guid? OperationId);

/// <param name="ServiceOrderItemId">Khóa idempotency của vé CLS.</param>
/// <param name="MedicalServiceId">scheduling.MedicalService; Scheduling chọn phòng thực hiện còn hoạt động.</param>
public sealed record ClsTicketLine(Guid ServiceOrderItemId, Guid MedicalServiceId);

public enum TicketKind
{
    InitialConsultation,
    CLS,
    ReturnConsultation,
}

/// <param name="ServiceOrderItemId">Có với vé CLS; null với vé quay lại.</param>
/// <param name="TargetRoomId">Phòng thực hiện (identity.Room): phòng CLS, hoặc phòng ban đầu của bác sĩ với vé quay lại.</param>
public sealed record QueueTicketDto(
    Guid QueueTicketId,
    TicketKind Kind,
    Guid EncounterId,
    Guid? ServiceOrderItemId,
    Guid TargetRoomId,
    string TicketNumber,
    int SequenceNumber,
    DateTime IssuedAtUtc);

/// <param name="ClsTickets">Mỗi dòng của lệnh một vé, theo thứ tự Lines.</param>
/// <param name="ReturnTicket">Vé quay lại của đợt chỉ định, chưa kích hoạt.</param>
public sealed record ClsTicketsCreated(IReadOnlyList<QueueTicketDto> ClsTickets, QueueTicketDto ReturnTicket);
