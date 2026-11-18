namespace Scheduling.Contracts;

// ---------- Tìm bác sĩ và suất trống (cùng dạng với DTO tạm của W2-05, PR #44) ----------

/// <param name="ServiceDate">Ngày phục vụ D theo giờ Việt Nam; bắt buộc.</param>
/// <param name="DoctorRankId">Lọc theo cấp bậc (identity.DoctorRank), null là mọi cấp.</param>
/// <param name="SpecialtyId">Lọc theo chuyên khoa, null là mọi chuyên khoa.</param>
/// <param name="Page">1..<see cref="MaxPage"/>.</param>
/// <param name="PageSize">1..<see cref="MaxPageSize"/>, mặc định 20.</param>
public sealed record DoctorSearchQuery(DateOnly ServiceDate, Guid? DoctorRankId = null, Guid? SpecialtyId = null,
    int Page = 1, int PageSize = 20)
{
    public const int MaxPage = 1000;
    public const int MaxPageSize = 50;
}

/// <summary>Một bác sĩ có phiên còn nhận đặt trong ngày D.</summary>
/// <param name="AvailableSlots">max(0, Capacity − AdmittedCount − HeldCount − LockedCancelledCount); 0 là hết suất.</param>
/// <param name="ExamFee">Phí khám ban đầu do Billing báo giá (không lấy từ client).</param>
/// <param name="Currency">"VND".</param>
/// <param name="TotalSlots">Capacity của phiên (tổng suất trong ngày).</param>
/// <param name="DoctorRankName">Tên cấp bậc để hiển thị; null khi danh bạ bác sĩ không cung cấp.</param>
/// <param name="SpecialtyName">Tên chuyên khoa để hiển thị; null khi danh bạ bác sĩ không cung cấp.</param>
public sealed record DoctorSearchItem(Guid DoctorId, string FullName, Guid DoctorRankId, Guid SpecialtyId,
    int AvailableSlots, decimal ExamFee, string Currency, int TotalSlots = 0, string? DoctorRankName = null,
    string? SpecialtyName = null);

/// <param name="HasMore">Còn trang sau.</param>
public sealed record DoctorSearchPage(DateOnly ServiceDate, int Page, int PageSize, bool HasMore,
    IReadOnlyList<DoctorSearchItem> Items);

// ---------- Check in ----------

/// <param name="PerformedByUserId">Tài khoản tiếp tân thực hiện (identity.AppUser), ghi AuditLog.</param>
/// <param name="OperationId">workflow.Operation của lệnh, nếu có; đi kèm sự kiện TicketCheckedIn.</param>
public sealed record CheckInAppointment(Guid AppointmentId, Guid PerformedByUserId, Guid? OperationId);

/// <summary>Kết quả check in.</summary>
/// <param name="CheckedInAtUtc">Giờ DB lúc check in có hiệu lực (lần đầu).</param>
/// <param name="AlreadyCheckedIn">true khi lịch đã check in từ trước; các trường trả lại giá trị của lần đầu.</param>
/// <param name="QueueTicketId">Vé khám đầu được tạo sau lời gọi Clinical đồng bộ trong cùng UoW (Q1 chốt 17/11/2026).</param>
/// <param name="TicketNumber">Mã vé gồm quotaId và số thứ tự; hiển thị số gọi bằng SequenceNumber.</param>
/// <param name="SequenceNumber">Thứ tự khám đầu trong phiên bác sĩ, cấp dưới khóa quota.</param>
/// <param name="EncounterId">Lượt khám do Clinical mở đồng bộ.</param>
public sealed record CheckInResult(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    DateOnly ServiceDate,
    DateTime CheckedInAtUtc,
    bool AlreadyCheckedIn,
    Guid QueueTicketId,
    string TicketNumber,
    int SequenceNumber,
    Guid EncounterId,
    string? DisplayNumber = null);

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
    DateTime IssuedAtUtc,
    string? DisplayNumber = null);

/// <param name="ClsTickets">Mỗi dòng của lệnh một vé, theo thứ tự Lines.</param>
/// <param name="ReturnTicket">Vé quay lại của đợt chỉ định, chưa kích hoạt.</param>
public sealed record ClsTicketsCreated(IReadOnlyList<QueueTicketDto> ClsTickets, QueueTicketDto ReturnTicket);

/// <summary>Thông tin đối soát và kiểm tra vé CLS theo phòng (GP §6.5).</summary>
public sealed record ClsQueueTicketValidationDto(
    Guid QueueTicketId,
    Guid RoomId,
    Guid EncounterId,
    Guid PatientId,
    Guid ServiceOrderItemId,
    Guid CLSResultId,
    string TicketNumber,
    string? PatientName,
    DateOnly? PatientDateOfBirth,
    bool IsServiceFinished,
    bool IsResultReturned);

