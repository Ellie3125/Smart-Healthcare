namespace Billing.Contracts;

public enum BookingPaymentStatus { Pending, Paid, Failed, Unknown, RefundPending, Refunded }
public sealed record BookingPaymentState(Guid BookingAttemptId, BookingPaymentStatus Status);
public sealed record PaidUnallocatedItem(Guid RefundRequestId, Guid PaymentIntentId, Guid? BookingAttemptId,
    decimal RequestedAmount, string Status, DateTime RequestedAtUtc);
public sealed record PaidUnallocatedPage(IReadOnlyList<PaidUnallocatedItem> Items, bool HasMore, Guid? NextCursor);

/// <summary>Read-only Billing projection. Caller checks authorization; never updates, refunds or queries another module's schema.</summary>
public interface IBillingStatusReader
{
    /// <summary>Unsettled PaidUnallocated refunds in SQL Server GUID order, cursor by RefundRequest PK; take 1–100, Guid.Empty starts the list.</summary>
    Task<PaidUnallocatedPage> GetPaidUnallocatedAsync(Guid afterRefundRequestId, int take, CancellationToken ct);
    /// <summary>All initial-consultation attempts for this booking; verified money wins over older failed/unknown status. Missing payments → Unknown.</summary>
    Task<BookingPaymentState> GetBookingPaymentStatusAsync(Guid bookingAttemptId, CancellationToken ct);
}
