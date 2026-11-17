namespace Billing.Contracts;

/// <summary>
/// Thu tiền mặt phí khám ban đầu tại quầy cho bệnh nhân đăng ký tại quầy (Phạm vi §5.1). Luồng đăng ký tại quầy của Scheduling
/// giữ chỗ, tạo khoản phí khám (<see cref="IBillingModule.CreateExamChargeAsync"/>) rồi gọi method này khi tiếp tân xác nhận đã
/// nhận tiền. Billing ghi phiếu thu và phát <see cref="PaymentVerified"/> (InitialConsultation, BookingAttemptId) qua Outbox,
/// nên lịch được xác nhận bằng đúng handler của luồng VNPAY. Tách khỏi <see cref="IBillingModule"/> để các fake hiện có không phải sửa.
/// </summary>
public interface ICounterExamFeeCollector
{
    /// <summary>
    /// Ghi nhận đã thu đủ phí khám bằng tiền mặt. Bên gọi đã mở unit of work có BillingDbContext thì tham gia transaction đó.
    /// Gọi lại cho cùng lượt đặt lịch và khoản phí trả phiếu thu cũ, <see cref="ExamFeeCashReceipt.AlreadyCollected"/> = true.
    /// Yêu cầu VNPAY đang mở của lượt đặt lịch chuyển Cancelled.
    /// </summary>
    /// <exception cref="BillingException">
    /// InvalidRequest khi thiếu Id; ChargeNotPayable khi khoản không phải phí khám, đã hủy, hoặc đã được trả bằng giao dịch khác.
    /// </exception>
    Task<ExamFeeCashReceipt> CollectExamFeeCashAsync(CollectExamFeeCash command, CancellationToken ct);
}

/// <param name="ChargeId">Khoản phí khám do <see cref="IBillingModule.CreateExamChargeAsync"/> tạo cho lượt đặt lịch này.</param>
/// <param name="ReceptionistAppUserId">Tài khoản tiếp tân nhận tiền; đối soát cuối ca theo tài khoản này.</param>
public sealed record CollectExamFeeCash(
    Guid BookingAttemptId,
    Guid PatientId,
    Guid ChargeId,
    Guid ReceptionistAppUserId,
    string? ClientIp = null);

/// <param name="ReceiptNumber">Số phiếu thu (PaymentTransaction.TransactionReference).</param>
public sealed record ExamFeeCashReceipt(
    string ReceiptNumber,
    Guid PaymentIntentId,
    Guid BookingAttemptId,
    Guid PatientId,
    Guid ChargeId,
    decimal Amount,
    string Currency,
    DateTime CollectedAtUtc,
    bool AlreadyCollected);

/// <summary>Yêu cầu hoàn tiền do module khác phát hiện (Phạm vi §5.6). Mọi yêu cầu chờ nhân viên duyệt trước khi chi.</summary>
public interface IPaymentRefunds
{
    /// <summary>
    /// Đã thu phí khám nhưng không giữ được suất (PaidUnallocated): tạo yêu cầu hoàn toàn bộ số đã thu của yêu cầu thanh toán
    /// <paramref name="paymentIntentId"/> và hủy khoản phí khám. Scheduling gọi trong unit of work của handler PaymentVerified.
    /// Gọi lặp không tạo yêu cầu thứ hai. Yêu cầu thanh toán chưa thành công thì không làm gì.
    /// </summary>
    /// <returns>Số yêu cầu hoàn vừa tạo.</returns>
    Task<int> RequestPaidUnallocatedRefundAsync(Guid paymentIntentId, CancellationToken ct);
}
