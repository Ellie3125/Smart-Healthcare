using System.Text.Json;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Billing.Application;

/// <summary>Một phần tiền của giao dịch thu thành công đã phân bổ vào một khoản.</summary>
public sealed record RefundablePayment(Guid PaymentTransactionId, Guid PaymentIntentId, Guid ChargeId, decimal AllocatedAmount);

/// <summary>Yêu cầu hoàn trên màn hình admin, tiếp tân và bệnh nhân. Mã trạng thái, lý do là chuỗi tên enum.</summary>
/// <param name="Status">Pending (chờ duyệt), Approved (đã duyệt, chờ chi), Rejected, Settled (đã chi).</param>
/// <param name="Reason">CancelledBefore0500, HospitalCancelled, PaidUnallocated, DuplicatePayment, SystemError.</param>
/// <param name="PayoutChannel">Kênh chi: hiện chỉ CashCounter (tiền mặt tại quầy).</param>
/// <param name="RefundSlipNumber">Số phiếu hoàn khi đã chi (RefundTransaction.ProviderRefundTransactionId).</param>
public sealed record RefundView(
    Guid RefundRequestId,
    string Status,
    string Reason,
    string PolicyRule,
    decimal Amount,
    string Currency,
    Guid ChargeId,
    string ChargeName,
    Guid PatientId,
    Guid? AppointmentId,
    string OriginalTransactionReference,
    string OriginalProvider,
    string PayoutChannel,
    DateTime RequestedAtUtc,
    DateTime? DecidedAtUtc,
    Guid? DecidedByUserId,
    string? RefundSlipNumber,
    DateTime? SettledAtUtc);

/// <summary>Lọc danh sách yêu cầu hoàn. Null là không lọc.</summary>
public sealed record RefundQuery(RefundRequestStatus? Status = null, Guid? PatientId = null, int Take = RefundService.ListLimit);

/// <summary>Một dòng shared.AuditLog do Billing ghi trong cùng transaction.</summary>
public sealed record BillingAuditEntry(
    Guid PerformedByUserId,
    string TableName,
    Guid RecordId,
    bool IsInsert,
    string ChangedColumns,
    string NewValuesJson,
    string ChangeReason,
    string? ClientIp);

/// <summary>Lưu và đọc yêu cầu hoàn (billing.RefundRequest, billing.RefundTransaction). Lệnh ghi chạy trong <see cref="IPaymentStore.InTransactionAsync{T}"/>.</summary>
public interface IRefundStore
{
    /// <summary>Phân bổ của các giao dịch thu Success vào <paramref name="chargeIds"/> (không khóa).</summary>
    Task<IReadOnlyList<RefundablePayment>> GetPaidAllocationsAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct);

    /// <summary>Phân bổ của giao dịch thu Success thuộc yêu cầu thanh toán; rỗng khi giao dịch chưa thành công.</summary>
    Task<IReadOnlyList<RefundablePayment>> GetPaidAllocationsOfIntentAsync(Guid paymentIntentId, CancellationToken ct);

    /// <summary>Yêu cầu hoàn chưa bị từ chối (Pending, Approved, Settled) của các khoản.</summary>
    Task<IReadOnlyList<RefundRequest>> GetActiveRequestsAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct);

    /// <summary>Khóa yêu cầu hoàn (UPDLOCK) tới hết transaction; null khi không có.</summary>
    Task<RefundRequest?> LockRequestAsync(Guid refundRequestId, CancellationToken ct);

    Task<IReadOnlyList<RefundView>> ListAsync(RefundQuery query, CancellationToken ct);

    Task<RefundView?> GetAsync(Guid refundRequestId, CancellationToken ct);

    void Add(RefundRequest request);

    void Add(RefundTransaction transaction);

    /// <summary>Ghi shared.AuditLog dùng chung transaction của Billing.</summary>
    Task WriteAuditAsync(BillingAuditEntry entry, CancellationToken ct);
}

public enum RefundChangeOutcome
{
    Done,
    NotFound,
    /// <summary>Trạng thái hiện tại không cho phép thao tác (đã duyệt/từ chối, chưa duyệt, đã chi).</summary>
    InvalidState,
}

public sealed record RefundChange(RefundChangeOutcome Outcome, RefundView? Refund, string? CurrentStatus = null);

/// <summary>Phiếu hoàn tiền mặt tại quầy.</summary>
public sealed record CashRefundSlip(
    string RefundSlipNumber,
    Guid RefundRequestId,
    Guid PatientId,
    decimal Amount,
    string Currency,
    DateTime DisbursedAtUtc);

/// <summary>
/// Hoàn tiền (Phạm vi §5.6, §6.2, §6.3). Hệ thống chỉ ghi chép: tiền nằm ở merchant hoặc két thu ngân, mọi khoản hoàn do nhân viên
/// duyệt. Luồng: tạo yêu cầu Pending (hủy lịch được hoàn, PaidUnallocated) → admin duyệt (Approved) hoặc từ chối (Rejected) →
/// tiếp tân chi tiền mặt, ghi phiếu hoàn (RefundTransaction Success, RefundRequest Settled, BillingItem.RefundedAmount).
/// Hoàn qua VNPAY chưa làm: API hoàn tiền của VNPAY sandbox trả 99 cho mọi request; mọi khoản hoàn chi tiền mặt tại quầy.
/// Với DecisionByStaffId lưu AppUserId của admin (token chưa có StaffId), như PayerAppUserId của phiếu thu.
/// Không phụ thuộc Identity: Worker (handler PaymentVerified, AppointmentCancelled) không nạp module Identity; endpoint bệnh nhân
/// tự kiểm quyền đại diện hồ sơ trước khi lọc theo PatientId.
/// </summary>
public sealed class RefundService(IPaymentStore store, IRefundStore refunds, IChargeStore charges, TimeProvider clock) : IPaymentRefunds
{
    public const int ListLimit = 200;
    public const int NoteMaxLength = 500;
    public const string CashCounter = "CashCounter";

    /// <summary>
    /// Hủy lịch có hiệu lực (AppointmentCancelled). Refund = Full: hủy khoản phí khám và tạo yêu cầu hoàn số đã thu, lý do
    /// HospitalCancelled khi bệnh viện hủy, ngược lại CancelledBefore0500. Refund = None: giữ nguyên khoản đã thu (Phạm vi §6.2).
    /// </summary>
    /// <returns>Số yêu cầu hoàn vừa tạo.</returns>
    public async Task<int> RequestForCancellationAsync(AppointmentCancelled e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Refund != ConsultationRefund.Full)
            return 0;

        var chargeIds = await charges.FindExamChargeIdsAsync(e.BookingAttemptId, e.AppointmentId, ct);
        if (chargeIds.Count == 0)
            return 0;

        var reason = e.CancelledBy == CancellationInitiator.Hospital ? RefundReason.HospitalCancelled : RefundReason.CancelledBefore0500;
        return await CreateRequestsAsync(chargeIds, paymentIntentId: null, reason, e.AppointmentId, ct);
    }

    public Task<int> RequestPaidUnallocatedRefundAsync(Guid paymentIntentId, CancellationToken ct) =>
        RequestForPaymentAsync(paymentIntentId, RefundReason.PaidUnallocated, ct);

    public Task<int> RequestDuplicatePaymentRefundAsync(Guid paymentIntentId, CancellationToken ct) =>
        RequestForPaymentAsync(paymentIntentId, RefundReason.DuplicatePayment, ct);

    private async Task<int> RequestForPaymentAsync(Guid paymentIntentId, RefundReason reason, CancellationToken ct)
    {
        if (paymentIntentId == Guid.Empty)
            throw new BillingException(BillingErrorCode.InvalidRequest, "Thiếu PaymentIntentId.");

        var paid = await refunds.GetPaidAllocationsOfIntentAsync(paymentIntentId, ct);
        if (paid.Count == 0)
            return 0;

        return await CreateRequestsAsync(paid.Select(p => p.ChargeId).Distinct().ToList(), paymentIntentId, reason, appointmentId: null, ct);
    }

    public Task<IReadOnlyList<RefundView>> ListAsync(RefundQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return refunds.ListAsync(query with { Take = Math.Clamp(query.Take, 1, ListLimit) }, ct);
    }

    /// <summary>Admin duyệt hoặc từ chối yêu cầu đang Pending. Ghi AuditLog kèm ghi chú.</summary>
    public async Task<RefundChange> DecideAsync(Guid refundRequestId, Guid adminUserId, bool approve, string? note, string? clientIp,
        CancellationToken ct)
    {
        if (adminUserId == Guid.Empty)
            throw new BillingException(BillingErrorCode.InvalidRequest, "Thiếu tài khoản admin.");
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note?.Length > NoteMaxLength)
            throw new BillingException(BillingErrorCode.InvalidRequest, $"Ghi chú tối đa {NoteMaxLength} ký tự.");

        var outcome = await store.InTransactionAsync(async token =>
        {
            var request = await refunds.LockRequestAsync(refundRequestId, token);
            if (request is null)
                return (RefundChangeOutcome.NotFound, (string?)null);
            if (request.Status != RefundRequestStatus.Pending)
                return (RefundChangeOutcome.InvalidState, request.Status.ToString());

            var now = clock.GetUtcNow().UtcDateTime;
            request.Status = approve ? RefundRequestStatus.Approved : RefundRequestStatus.Rejected;
            request.DecisionByStaffId = adminUserId;
            request.DecidedAtUtc = now;
            await store.SaveChangesAsync(token);
            await refunds.WriteAuditAsync(new BillingAuditEntry(adminUserId, "billing.RefundRequest", request.Id, IsInsert: false,
                "Status,DecisionByStaffId,DecidedAtUtc",
                JsonSerializer.Serialize(new { status = request.Status.ToString(), amount = request.RequestedAmount, note }),
                approve ? "Duyệt hoàn tiền" : "Từ chối hoàn tiền", clientIp), token);
            return (RefundChangeOutcome.Done, (string?)null);
        }, ct);

        return await ResultAsync(refundRequestId, outcome, ct);
    }

    /// <summary>
    /// Tiếp tân chi tiền mặt cho yêu cầu đã duyệt: phiếu hoàn (RefundTransaction Success), RefundRequest Settled, cộng
    /// RefundedAmount của khoản, AuditLog; tất cả trong một transaction. Mỗi yêu cầu chi đúng một lần (khóa dòng + unique RefundRequestId).
    /// </summary>
    public async Task<(RefundChange Change, CashRefundSlip? Slip)> DisburseCashAsync(Guid refundRequestId, Guid staffUserId, string? clientIp,
        CancellationToken ct)
    {
        if (staffUserId == Guid.Empty)
            throw new BillingException(BillingErrorCode.InvalidRequest, "Thiếu tài khoản tiếp tân.");

        var (outcome, slip) = await store.InTransactionAsync(async token =>
        {
            var request = await refunds.LockRequestAsync(refundRequestId, token);
            if (request is null)
                return ((RefundChangeOutcome.NotFound, (string?)null), (CashRefundSlip?)null);
            if (request.Status != RefundRequestStatus.Approved)
                return ((RefundChangeOutcome.InvalidState, request.Status.ToString()), null);

            var charge = (await store.LockChargesAsync([request.BillingItemId], token)).Single();
            var now = clock.GetUtcNow().UtcDateTime;
            var slipNumber = $"PH{now + TimeSpan.FromHours(7):yyyyMMdd}{Guid.NewGuid():N}"[..22].ToUpperInvariant();
            var transaction = new RefundTransaction
            {
                RefundRequestId = request.Id,
                OriginalPaymentTransactionId = request.OriginalPaymentTransactionId,
                ProviderRefundTransactionId = slipNumber,
                RefundedAmount = request.RequestedAmount,
                Currency = BillingCurrency.Vnd,
                Status = RefundTransactionStatus.Success,
                ProcessedAtUtc = now,
                ProviderResponsePayload = JsonSerializer.Serialize(new { channel = CashCounter, disbursedBy = staffUserId }),
            };
            refunds.Add(transaction);
            request.Status = RefundRequestStatus.Settled;
            charge.RefundedAmount += request.RequestedAmount;
            await store.SaveChangesAsync(token);

            var patientId = (await refunds.GetAsync(request.Id, token))!.PatientId;
            await refunds.WriteAuditAsync(new BillingAuditEntry(staffUserId, "billing.RefundTransaction", transaction.Id, IsInsert: true,
                "RefundedAmount,Status,ProcessedAtUtc",
                JsonSerializer.Serialize(new
                {
                    refundSlipNumber = slipNumber,
                    refundRequestId = request.Id,
                    amount = request.RequestedAmount,
                    currency = BillingCurrency.Vnd,
                    chargeId = request.BillingItemId,
                    patientId,
                }),
                "Hoàn tiền mặt tại quầy", clientIp), token);
            return ((RefundChangeOutcome.Done, (string?)null),
                new CashRefundSlip(slipNumber, request.Id, patientId, request.RequestedAmount, BillingCurrency.Vnd, now));
        }, ct);

        return (await ResultAsync(refundRequestId, outcome, ct), slip);
    }

    // Khóa các khoản (cùng thứ tự Id với luồng thanh toán) rồi mới đọc yêu cầu đã có: sự kiện giao lặp hoặc hai nguồn cùng lúc
    // không tạo yêu cầu thứ hai cho cùng phần tiền.
    private Task<int> CreateRequestsAsync(IReadOnlyList<Guid> chargeIds, Guid? paymentIntentId, RefundReason reason, Guid? appointmentId,
        CancellationToken ct) =>
        store.InTransactionAsync(async token =>
        {
            var locked = await store.LockChargesAsync(chargeIds.Order().ToList(), token);
            var exam = locked.Where(c => c.ItemType == ItemType.ConsultationFee).ToDictionary(c => c.Id);
            var paid = (await refunds.GetPaidAllocationsAsync(exam.Keys.ToList(), token))
                .Where(p => paymentIntentId is null || p.PaymentIntentId == paymentIntentId)
                .ToList();
            var requested = (await refunds.GetActiveRequestsAsync(exam.Keys.ToList(), token))
                .GroupBy(r => (r.OriginalPaymentTransactionId, r.BillingItemId))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.RequestedAmount));

            var now = clock.GetUtcNow().UtcDateTime;
            var created = 0;
            foreach (var allocation in paid)
            {
                var remaining = allocation.AllocatedAmount
                    - requested.GetValueOrDefault((allocation.PaymentTransactionId, allocation.ChargeId));
                if (remaining <= 0m)
                    continue;

                refunds.Add(new RefundRequest
                {
                    BillingItemId = allocation.ChargeId,
                    OriginalPaymentTransactionId = allocation.PaymentTransactionId,
                    AppointmentId = appointmentId ?? exam[allocation.ChargeId].AppointmentId,
                    RequestedAmount = remaining,
                    RefundReason = reason,
                    PolicyRuleApplied = reason == RefundReason.CancelledBefore0500 ? PolicyRuleApplied.FullRefundPre0500 : PolicyRuleApplied.CustomApproval,
                    Status = RefundRequestStatus.Pending,
                    RequestedAtUtc = now,
                });
                created++;
            }

            // Lịch đã hủy (hoặc chưa từng có): phí khám không còn là nghĩa vụ; tiền đã thu thành tiền chờ hoàn trong tổng hợp chi phí.
            // Hoàn theo một lần trả: chỉ hủy khoản mà toàn bộ tiền đang giữ đến từ lần trả này, để không hủy phí của lịch đang dùng.
            foreach (var charge in exam.Values.Where(c => c.IsInEffect && paid.Any(p => p.ChargeId == c.Id)))
            {
                var fromThisPayment = paid.Where(p => p.ChargeId == charge.Id).Sum(p => p.AllocatedAmount);
                if (paymentIntentId is null || charge.PaidAmount - charge.RefundedAmount <= fromThisPayment)
                    charge.Status = BillingItemStatus.Cancelled;
            }

            await store.SaveChangesAsync(token);
            return created;
        }, ct);

    private async Task<RefundChange> ResultAsync(Guid refundRequestId, (RefundChangeOutcome Outcome, string? CurrentStatus) result,
        CancellationToken ct) =>
        result.Outcome == RefundChangeOutcome.NotFound
            ? new RefundChange(RefundChangeOutcome.NotFound, null)
            : new RefundChange(result.Outcome, await refunds.GetAsync(refundRequestId, ct), result.CurrentStatus);
}

/// <summary>Billing nhận AppointmentCancelled qua Outbox: hủy lịch được hoàn thì tạo yêu cầu hoàn phí khám.</summary>
public sealed class AppointmentCancelledHandler(RefundService refunds) : IIntegrationEventHandler<AppointmentCancelled>
{
    public Task HandleAsync(AppointmentCancelled e, CancellationToken ct) => refunds.RequestForCancellationAsync(e, ct);
}
