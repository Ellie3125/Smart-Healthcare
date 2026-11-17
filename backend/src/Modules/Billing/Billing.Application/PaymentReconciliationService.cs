using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Application;

/// <summary>Hỏi trạng thái giao dịch ở cổng (VNPAY querydr). Gọi qua mạng: không gọi bên trong transaction DB (MOD-05).</summary>
public interface IPaymentStatusGateway
{
    /// <summary>Đã có đủ cấu hình để hỏi cổng. Chưa cấu hình thì cũng chưa thể tạo liên kết thanh toán nào.</summary>
    bool IsConfigured { get; }

    /// <exception cref="PaymentGatewayException">Lỗi mạng, chữ ký phản hồi sai, hoặc cổng từ chối truy vấn.</exception>
    Task<GatewayPaymentStatus> QueryAsync(GatewayStatusQuery query, CancellationToken ct);
}

/// <param name="CreatedAtUtc">PaymentIntent.CreatedAtUtc, cùng giá trị đã gửi làm vnp_CreateDate khi tạo liên kết.</param>
public sealed record GatewayStatusQuery(string TransactionReference, DateTime CreatedAtUtc, string Description);

public enum GatewayPaymentState
{
    /// <summary>Cổng xác nhận đã thu tiền.</summary>
    Paid,

    /// <summary>Cổng xác nhận giao dịch lỗi, không thu tiền.</summary>
    Failed,

    /// <summary>Giao dịch chưa hoàn tất ở cổng.</summary>
    Pending,

    /// <summary>Cổng không có giao dịch với mã tham chiếu này (người trả chưa vào trang thanh toán).</summary>
    NotFound,

    /// <summary>Trạng thái cần người xử lý: giao dịch đảo (đã trừ tiền ở ngân hàng nhưng chưa thành công ở cổng), nghi gian lận.</summary>
    NeedsReview,
}

/// <param name="Amount">Số tiền cổng báo (đã chia 100 với VNPAY); null khi cổng không có giao dịch.</param>
/// <param name="GatewayCode">Mã trạng thái gốc của cổng, để hiển thị và đối soát.</param>
/// <param name="RawData">Phản hồi thô đã xác minh chữ ký.</param>
public sealed record GatewayPaymentStatus(
    GatewayPaymentState State,
    string? ProviderTransactionId,
    decimal? Amount,
    DateTime? PaidAtUtc,
    string GatewayCode,
    string RawData);

/// <summary>Một yêu cầu thanh toán online cần người xử lý (Unknown).</summary>
public sealed record PaymentNeedingReview(
    string TransactionReference,
    Guid PaymentIntentId,
    string IntentNumber,
    PaymentPurpose Purpose,
    Guid PatientId,
    decimal Amount,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

/// <summary>Truy vấn phục vụ đối soát thanh toán online.</summary>
public interface IReconciliationStore
{
    /// <summary>Mã tham chiếu của yêu cầu VNPAY còn Pending/Processing, giao dịch còn Pending, đã hết hạn trước <paramref name="expiredBeforeUtc"/>; hết hạn sớm nhất trước.</summary>
    Task<IReadOnlyList<string>> FindOverdueAsync(DateTime expiredBeforeUtc, int take, CancellationToken ct);

    /// <summary>Đọc (không khóa) yêu cầu và giao dịch theo mã tham chiếu.</summary>
    Task<(PaymentIntent Intent, PaymentTransaction Transaction)?> FindByReferenceAsync(string transactionReference, CancellationToken ct);

    /// <summary>Yêu cầu VNPAY ở trạng thái Unknown, mới nhất trước.</summary>
    Task<IReadOnlyList<PaymentNeedingReview>> ListNeedingReviewAsync(int take, CancellationToken ct);
}

public enum ReconcileOutcome
{
    /// <summary>Cổng báo đã thu tiền; vừa ghi nhận như IPN (PaymentVerified vào Outbox).</summary>
    PaidRecorded,

    /// <summary>Đã ghi nhận thành công từ trước (IPN đã về).</summary>
    AlreadyPaid,

    /// <summary>Cổng báo lỗi; giao dịch và yêu cầu chuyển Failed.</summary>
    FailedRecorded,

    /// <summary>Giao dịch đã có kết quả cuối từ trước, hoặc là phiếu thu tại quầy; không hỏi cổng.</summary>
    AlreadyFinal,

    /// <summary>Quá hạn mà cổng không có giao dịch; yêu cầu chuyển Expired.</summary>
    Expired,

    /// <summary>Chưa có kết quả; hỏi lại sau.</summary>
    StillPending,

    /// <summary>Cần người xử lý; yêu cầu chuyển Unknown.</summary>
    NeedsReview,

    /// <summary>Không có giao dịch với mã tham chiếu này trong hệ thống.</summary>
    NotFound,

    /// <summary>Không hỏi được cổng (mạng, cấu hình, cổng từ chối); thử lại sau.</summary>
    GatewayUnavailable,
}

public sealed record PaymentReconciliation(
    string TransactionReference,
    ReconcileOutcome Outcome,
    Guid? PaymentIntentId,
    PaymentState? Status,
    decimal? Amount,
    string? ProviderTransactionId,
    string? GatewayCode,
    string? Detail = null);

public sealed record ReconcileRun(int Checked, int Paid, int Failed, int Expired, int NeedsReview, int Unavailable);

/// <summary>
/// Đối soát thanh toán online (Giải pháp kỹ thuật v2.13 mục 5.3, 5.5): yêu cầu VNPAY quá hạn mà chưa có IPN thì hỏi cổng
/// bằng querydr theo vnp_TxnRef; tiếp tân cũng tra được theo mã giao dịch khi bệnh nhân mang bill đến quầy. Kết quả "đã thu"
/// đi qua cùng luồng ghi nhận với IPN (<see cref="PaymentService.ConfirmVerifiedAsync"/>), không ghi nhận chỉ dựa vào ảnh bill.
/// Giao dịch của yêu cầu hết hạn giữ Pending: nếu cổng vẫn thu tiền rồi gửi IPN muộn, tiền vẫn được ghi nhận.
/// </summary>
public sealed class PaymentReconciliationService(
    IReconciliationStore queries,
    IPaymentStore store,
    PaymentService payments,
    IPaymentStatusGateway gateway,
    TimeProvider clock)
{
    /// <summary>Chờ thêm sau hạn để IPN kịp về trước khi hỏi cổng.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    /// <summary>Quá hạn lâu hơn mà cổng vẫn báo chưa hoàn tất thì chuyển người xử lý.</summary>
    public static readonly TimeSpan ReviewAfter = TimeSpan.FromMinutes(30);

    public const int BatchSize = 50;

    /// <summary>Job Worker: đối soát các yêu cầu VNPAY đã quá hạn. Lỗi của một yêu cầu không chặn các yêu cầu khác.</summary>
    public async Task<ReconcileRun> ReconcileOverdueAsync(CancellationToken ct)
    {
        var due = await queries.FindOverdueAsync(clock.GetUtcNow().UtcDateTime - Grace, BatchSize, ct);
        var results = new List<ReconcileOutcome>(due.Count);
        foreach (var reference in due)
            results.Add((await ReconcileAsync(reference, ct)).Outcome);

        return new ReconcileRun(
            results.Count,
            results.Count(r => r is ReconcileOutcome.PaidRecorded or ReconcileOutcome.AlreadyPaid),
            results.Count(r => r == ReconcileOutcome.FailedRecorded),
            results.Count(r => r == ReconcileOutcome.Expired),
            results.Count(r => r == ReconcileOutcome.NeedsReview),
            results.Count(r => r == ReconcileOutcome.GatewayUnavailable));
    }

    /// <summary>Hỏi cổng về một giao dịch và ghi nhận kết quả. Gọi lại được.</summary>
    public async Task<PaymentReconciliation> ReconcileAsync(string transactionReference, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transactionReference))
            throw new BillingException(BillingErrorCode.InvalidRequest, "Thiếu mã giao dịch.");

        var reference = transactionReference.Trim();
        if (await queries.FindByReferenceAsync(reference, ct) is not { } found)
            return new PaymentReconciliation(reference, ReconcileOutcome.NotFound, null, null, null, null, null);

        var (intent, transaction) = found;
        if (intent.Provider != PaymentIntentProvider.VNPAY || transaction.PaymentStatus != PaymentStatus.Pending)
            return Result(ReconcileOutcome.AlreadyFinal, intent, transaction);

        var now = clock.GetUtcNow().UtcDateTime;
        var overdue = intent.ExpiresAtUtc <= now;
        // Thiếu cấu hình (ví dụ Worker chưa có khóa VNPAY) thì không đoán: tiền có thể đã thu mà IPN thất lạc. Yêu cầu giữ nguyên,
        // tạo liên kết mới vẫn được vì yêu cầu quá hạn không còn được coi là đang mở.
        if (!gateway.IsConfigured)
            return Result(ReconcileOutcome.GatewayUnavailable, intent, transaction) with { Detail = "Chưa cấu hình cổng thanh toán." };

        GatewayPaymentStatus status;
        try
        {
            status = await gateway.QueryAsync(new GatewayStatusQuery(reference, intent.CreatedAtUtc, $"Truy van {intent.IntentNumber}"), ct);
        }
        catch (PaymentGatewayException ex)
        {
            // Hỏi mãi không được (cổng lỗi kéo dài, giao dịch cổng không trả lời được) thì dừng hỏi tự động, chuyển người xử lý.
            if (now - intent.ExpiresAtUtc >= ReviewAfter)
                return await SetIntentStatusAsync(reference, PaymentIntentStatus.Unknown, ReconcileOutcome.NeedsReview, ct) with { Detail = ex.Message };
            return Result(ReconcileOutcome.GatewayUnavailable, intent, transaction) with { Detail = ex.Message };
        }

        switch (status.State)
        {
            case GatewayPaymentState.Paid when status is { ProviderTransactionId: { } providerId, Amount: { } amount }:
                try
                {
                    var confirmed = await payments.ConfirmVerifiedAsync(new VerifiedPayment(
                        reference, providerId, providerId, amount, BillingCurrency.Vnd, status.PaidAtUtc ?? now, status.RawData), ct);
                    return await ReloadAsync(reference, confirmed.AlreadyRecorded ? ReconcileOutcome.AlreadyPaid : ReconcileOutcome.PaidRecorded,
                        status.GatewayCode, ct);
                }
                catch (BillingException ex) when (ex.Code is BillingErrorCode.AmountMismatch or BillingErrorCode.PaymentConflict)
                {
                    // Cổng báo đã thu nhưng không khớp số tiền hoặc mã giao dịch: không tự ghi nhận, chuyển người xử lý.
                    return await SetIntentStatusAsync(reference, PaymentIntentStatus.Unknown, ReconcileOutcome.NeedsReview, ct, status.GatewayCode);
                }

            case GatewayPaymentState.Failed:
                var failure = await payments.RecordFailedAsync(new FailedPayment(
                    reference, status.ProviderTransactionId, $"{reference}-querydr-{status.GatewayCode}", transaction.TransactionAmount,
                    BillingCurrency.Vnd, now, status.RawData), ct);
                return await ReloadAsync(reference,
                    failure.Outcome == PaymentFailureOutcome.IgnoredAlreadyPaid ? ReconcileOutcome.AlreadyPaid : ReconcileOutcome.FailedRecorded,
                    status.GatewayCode, ct);

            case GatewayPaymentState.NotFound when overdue:
                return await SetIntentStatusAsync(reference, PaymentIntentStatus.Expired, ReconcileOutcome.Expired, ct, status.GatewayCode);

            case GatewayPaymentState.Pending when now - intent.ExpiresAtUtc >= ReviewAfter:
            case GatewayPaymentState.NeedsReview:
            case GatewayPaymentState.Paid:
                return await SetIntentStatusAsync(reference, PaymentIntentStatus.Unknown, ReconcileOutcome.NeedsReview, ct, status.GatewayCode);

            default:
                return Result(ReconcileOutcome.StillPending, intent, transaction, status.GatewayCode);
        }
    }

    public Task<IReadOnlyList<PaymentNeedingReview>> ListNeedingReviewAsync(CancellationToken ct) => queries.ListNeedingReviewAsync(200, ct);

    // Đổi trạng thái yêu cầu khi giao dịch vẫn Pending (khóa rồi kiểm lại: IPN có thể vừa về).
    private async Task<PaymentReconciliation> SetIntentStatusAsync(string reference, PaymentIntentStatus target, ReconcileOutcome outcome,
        CancellationToken ct, string? gatewayCode = null)
    {
        var changed = await store.InTransactionAsync(async token =>
        {
            var transaction = await store.LockTransactionAsync(reference, token);
            if (transaction is not { PaymentStatus: PaymentStatus.Pending })
                return false;

            var intent = await store.GetIntentAsync(transaction.PaymentIntentId, token);
            if (intent.Status is not (PaymentIntentStatus.Pending or PaymentIntentStatus.Processing or PaymentIntentStatus.Unknown)
                || intent.Status == target)
                return intent.Status == target;

            intent.Status = target;
            await store.SaveChangesAsync(token);
            return true;
        }, ct);

        return await ReloadAsync(reference, changed ? outcome : ReconcileOutcome.AlreadyFinal, gatewayCode, ct);
    }

    private async Task<PaymentReconciliation> ReloadAsync(string reference, ReconcileOutcome outcome, string? gatewayCode, CancellationToken ct) =>
        await queries.FindByReferenceAsync(reference, ct) is { } current
            ? Result(outcome, current.Intent, current.Transaction, gatewayCode)
            : new PaymentReconciliation(reference, outcome, null, null, null, null, gatewayCode);

    private static PaymentReconciliation Result(ReconcileOutcome outcome, PaymentIntent intent, PaymentTransaction transaction,
        string? gatewayCode = null) =>
        new(transaction.TransactionReference, outcome, intent.Id, PaymentService.StateOf(intent), transaction.TransactionAmount,
            transaction.ProviderTransactionId, gatewayCode);
}
