using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Xunit;

namespace Billing.Tests;

public class PaymentCheckoutServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Calls_the_gateway_with_server_values_and_writes_nothing()
    {
        var store = new CheckoutStore();
        var gateway = new FakeGateway { Returns = new GatewayCheckout("http://gateway.test/pay") };

        var checkout = await Service(store, gateway).StartAsync(store.Intent.Id, "203.0.113.7", Ct);

        Assert.Equal("http://gateway.test/pay", checkout.PaymentUrl);
        Assert.Null(store.Transaction.ProviderTransactionId);
        Assert.Equal(0, store.Saves);
        var sent = Assert.Single(gateway.Requests);
        Assert.Equal(store.Transaction.TransactionReference, sent.TransactionReference);
        Assert.Equal(150_000m, sent.Amount);
        Assert.Equal(store.Intent.CreatedAtUtc, sent.CreatedAtUtc);
        Assert.Equal(store.Intent.ExpiresAtUtc, sent.ExpiresAtUtc);
        Assert.Equal("Thanh toan phi kham ban dau PI-20261105-ABC", sent.Description);
        Assert.Equal("203.0.113.7", sent.CustomerIp);
    }

    [Fact]
    public async Task Provider_without_an_adapter_is_rejected()
    {
        var store = new CheckoutStore();
        store.Intent.Provider = PaymentIntentProvider.Counter;
        var gateway = new FakeGateway();

        var error = await Assert.ThrowsAsync<BillingException>(() => Service(store, gateway).StartAsync(store.Intent.Id, null, Ct));

        Assert.Equal(BillingErrorCode.InvalidRequest, error.Code);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Gateway_failure_keeps_the_request_and_reports_gateway_unavailable()
    {
        var store = new CheckoutStore();
        var gateway = new FakeGateway { Throws = new PaymentGatewayException("VNPAY chưa cấu hình.") };

        var error = await Assert.ThrowsAsync<BillingException>(() => Service(store, gateway).StartAsync(store.Intent.Id, null, Ct));

        Assert.Equal(BillingErrorCode.GatewayUnavailable, error.Code);
        Assert.Null(store.Transaction.ProviderTransactionId);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task Refuses_to_call_the_gateway_inside_a_database_transaction()
    {
        var store = new CheckoutStore { HasActiveTransaction = true };
        var gateway = new FakeGateway();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(store, gateway).StartAsync(store.Intent.Id, null, Ct));

        Assert.Empty(gateway.Requests);
    }

    [Theory]
    [InlineData(PaymentIntentStatus.Succeeded, PaymentStatus.Success, 15)]
    [InlineData(PaymentIntentStatus.Failed, PaymentStatus.Failed, 15)]
    [InlineData(PaymentIntentStatus.Pending, PaymentStatus.Pending, -1)]
    public async Task Paid_failed_or_expired_request_is_not_sent_to_the_gateway(PaymentIntentStatus intentStatus, PaymentStatus transactionStatus, int minutesLeft)
    {
        var store = new CheckoutStore();
        store.Intent.Status = intentStatus;
        store.Transaction.PaymentStatus = transactionStatus;
        store.Intent.ExpiresAtUtc = Now.AddMinutes(minutesLeft);
        var gateway = new FakeGateway();

        var error = await Assert.ThrowsAsync<BillingException>(() => Service(store, gateway).StartAsync(store.Intent.Id, null, Ct));

        Assert.Equal(BillingErrorCode.PaymentConflict, error.Code);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Unknown_request_is_reported()
    {
        var error = await Assert.ThrowsAsync<BillingException>(() => Service(new CheckoutStore(), new FakeGateway()).StartAsync(Guid.NewGuid(), null, Ct));

        Assert.Equal(BillingErrorCode.PaymentNotFound, error.Code);
    }

    private static PaymentCheckoutService Service(CheckoutStore store, FakeGateway gateway) => new(store, gateway, new FixedClock(Now));

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeGateway : IPaymentGateway
    {
        public GatewayCheckout Returns { get; init; } = new("http://gateway.test/pay");

        public Exception? Throws { get; init; }

        public List<GatewayCheckoutRequest> Requests { get; } = [];

        public Task<GatewayCheckout> CreateCheckoutAsync(GatewayCheckoutRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Throws is null ? Task.FromResult(Returns) : Task.FromException<GatewayCheckout>(Throws);
        }
    }

    /// <summary>Một yêu cầu thanh toán phí khám đang chờ trong bộ nhớ; chỉ cài các hàm PaymentCheckoutService dùng.</summary>
    private sealed class CheckoutStore : IPaymentStore
    {
        public PaymentIntent Intent { get; } = new()
        {
            Id = Guid.NewGuid(),
            IntentNumber = "PI-20261105-ABC",
            PaymentType = PaymentType.InitialConsultation,
            Provider = PaymentIntentProvider.VNPAY,
            Status = PaymentIntentStatus.Pending,
            Currency = "VND",
            TotalExpectedAmount = 150_000m,
            CreatedAtUtc = Now.AddMinutes(-3),
            ExpiresAtUtc = Now.AddMinutes(15),
        };

        public PaymentTransaction Transaction { get; } = new()
        {
            Id = Guid.NewGuid(),
            TransactionReference = "REF0001",
            TransactionAmount = 150_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Pending,
        };

        public int Saves { get; private set; }

        public bool HasActiveTransaction { get; init; }

        public Task<(PaymentIntent Intent, PaymentTransaction Transaction)?> FindPaymentAsync(Guid paymentIntentId, CancellationToken ct) =>
            Task.FromResult<(PaymentIntent, PaymentTransaction)?>(paymentIntentId == Intent.Id ? (Intent, Transaction) : null);

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<PaymentTransaction?> LockTransactionAsync(string transactionReference, CancellationToken ct) =>
            Task.FromResult(transactionReference == Transaction.TransactionReference ? Transaction : null);

        public Task<bool> IsProviderTransactionIdTakenAsync(string providerTransactionId, Guid exceptTransactionId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task SaveChangesAsync(CancellationToken ct)
        {
            Saves++;
            return Task.CompletedTask;
        }

        private static InvalidOperationException Unused() => new("PaymentCheckoutService không dùng hàm này.");
        public Task<IReadOnlyList<BillingItem>> LockChargesAsync(IReadOnlyList<Guid> chargeIds, CancellationToken ct) => throw Unused();
        public Task LockBookingAttemptAsync(Guid bookingAttemptId, CancellationToken ct) => throw Unused();
        public Task<IReadOnlyList<OpenPayment>> FindOpenPaymentsAsync(IReadOnlyCollection<Guid> chargeIds, Guid? bookingAttemptId, DateTime nowUtc, CancellationToken ct) => throw Unused();
        public void Add(PaymentIntent intent, PaymentTransaction transaction, IReadOnlyList<PaymentAllocation> allocations) => throw Unused();
        public Task<PaymentIntent> GetIntentAsync(Guid paymentIntentId, CancellationToken ct) => throw Unused();
        public Task<bool> HasEventAsync(PaymentEventProvider provider, string eventId, CancellationToken ct) => throw Unused();
        public void AddEvent(PaymentEvent paymentEvent) => throw Unused();
        public Task<IReadOnlyList<PaymentAllocation>> GetAllocationsAsync(Guid paymentTransactionId, CancellationToken ct) => throw Unused();
        public Task EnqueueAsync(PaymentVerified integrationEvent, CancellationToken ct) => throw Unused();
    }
}
