namespace Scheduling.Contracts;

/// <summary>
/// Contract trong tiến trình do Scheduling công bố (docs/api/scheduling.md, v0.1 Draft, chờ H-02).
/// Module khác chỉ tham chiếu project này. Lỗi nghiệp vụ ném <see cref="SchedulingException"/> với <see cref="SchedulingErrorCode"/>.
/// Chưa có bản cài đặt: tra suất trống thuộc W2-05, check in W3-02, tạo vé CLS + vé quay lại W3-04.
/// </summary>
public interface ISchedulingModule
{
    /// <summary>
    /// Tra suất trống của nhiều bác sĩ trong một ngày phục vụ, mỗi bác sĩ một dòng theo thứ tự yêu cầu.
    /// Suất trống = max(0, Capacity − AdmittedCount − HeldCount − LockedCancelledCount) (Phạm vi §4.3).
    /// </summary>
    /// <remarks>
    /// Bên gọi: API tìm bác sĩ của Scheduling (W2-05, web bệnh nhân và chatbot qua REST).
    /// Chỉ đọc, không khóa, không cần <c>IUnitOfWork</c>; con số mang tính tham khảo, giữ chỗ (W2-02) kiểm lại dưới khóa.
    /// <see cref="AvailabilityStatus.CutoffPassed"/> so với giờ DB (<c>IDbClock</c>) tại lúc đọc.
    /// Idempotency: không áp dụng (chỉ đọc).
    /// Lỗi: <see cref="SchedulingErrorCode.InvalidInput"/> khi danh sách rỗng, có Guid rỗng, trùng, hoặc quá <see cref="GetDoctorAvailability.MaxDoctors"/>.
    /// </remarks>
    Task<IReadOnlyList<DoctorDayAvailability>> GetDoctorAvailabilityAsync(GetDoctorAvailability query, CancellationToken ct);

    /// <summary>
    /// Check in lịch hẹn đã xác nhận: Appointment Scheduled → CheckedIn, phát <see cref="TicketCheckedIn"/> trong cùng transaction.
    /// </summary>
    /// <remarks>
    /// Bên gọi: endpoint tiếp tân của Scheduling (W3-02) và luồng đăng ký tại quầy (W3-01).
    /// Điều kiện: Appointment Scheduled, ngày phục vụ là ngày D theo giờ Việt Nam, giờ DB chưa qua CheckInDeadlineUtc của phiên.
    /// Tự mở <c>IUnitOfWork</c>; gọi bên trong unit of work của bên gọi thì tham gia transaction đó.
    /// Idempotency: theo AppointmentId. Appointment đã CheckedIn thì trả kết quả cũ với <see cref="CheckInResult.AlreadyCheckedIn"/> = true,
    /// không phát sự kiện lần hai.
    /// Lỗi: AppointmentNotFound, InvalidAppointmentState (Cancelled, Absent, Rescheduled, Completed),
    /// CheckInNotOpen (chưa tới ngày D), CheckInDeadlinePassed.
    /// Vé khám đầu (số thứ tự) phụ thuộc câu hỏi mở "vé khám đầu có trước hay sau Encounter" (docs/api/scheduling.md §6).
    /// </remarks>
    Task<CheckInResult> CheckInAsync(CheckInAppointment command, CancellationToken ct);

    /// <summary>
    /// Tạo vé CLS cho từng dịch vụ của đợt chỉ định và một vé quay lại cho đợt đó (Phạm vi §5.2, Giải pháp §8.2.1).
    /// Vé CLS chưa được gọi tới khi khoản phí tương ứng đã trả (PaymentVerified, CLSOrderBatch);
    /// vé quay lại chỉ được kích hoạt khi đủ kết quả bắt buộc (Scheduling xử lý ResultPublished, W3-06), không qua method này.
    /// </summary>
    /// <remarks>
    /// Bên gọi: Clinical khi bác sĩ lập chỉ định CLS (OrderClsService).
    /// BẮT BUỘC chạy trong <c>IUnitOfWork</c> của Clinical, cùng transaction với đợt chỉ định và khoản phí CLS của Billing;
    /// không có transaction đang mở thì ném InvalidOperationException. Không tự commit, không gọi dịch vụ ngoài.
    /// Scheduling tự chọn phòng thực hiện còn hoạt động của từng dịch vụ (MedicalServiceRoom, ServiceStation.IsActive).
    /// Idempotency: vé CLS duy nhất theo ServiceOrderItemId, vé quay lại duy nhất theo ServiceOrderId; gọi lặp trả vé đã có, không tạo thêm.
    /// Lỗi: InvalidInput, InitialTicketNotFound (EncounterId không có vé khám đầu đã check in),
    /// MedicalServiceUnavailable (dịch vụ không tồn tại, ngừng hoạt động hoặc không còn phòng hoạt động).
    /// </remarks>
    Task<ClsTicketsCreated> CreateClsTicketsAsync(CreateClsTickets command, CancellationToken ct);
}
