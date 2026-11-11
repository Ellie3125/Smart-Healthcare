using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using IdentityPatients.Contracts;
using Xunit;

namespace Billing.Tests;

public class PatientPaymentServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Payer = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Payer_with_booking_access_sees_the_status_from_the_server()
    {
        var store = new Store();

        var status = await Service(store).GetAsync(store.Intent.Id, Payer, Ct);

        Assert.NotNull(status);
        Assert.Equal(store.Intent.Id, status.PaymentIntentId);
        Assert.Equal("PI-20261105-ABC", status.IntentNumber);
        Assert.Equal(PaymentPurpose.InitialConsultation, status.Purpose);
        Assert.Equal(150_000m, status.Amount);
        Assert.Equal("VND", status.Currency);
        Assert.Equal(PaymentState.Pending, status.Status);
        Assert.Equal(store.Intent.ExpiresAtUtc, status.ExpiresAtUtc);
        Assert.Null(status.PaidAtUtc);
        Assert.True(status.CanPay);
    }

    [Fact]
    public async Task Verified_payment_reports_paid_time_and_cannot_be_paid_again()
    {
        var store = new Store();
        store.Intent.Status = PaymentIntentStatus.Succeeded;
        store.Transaction.PaymentStatus = PaymentStatus.Success;
        store.Transaction.VerifiedAtUtc = Now.AddMinutes(-1);

        var status = await Service(store).GetAsync(store.Intent.Id, Payer, Ct);

        Assert.NotNull(status);
        Assert.Equal(PaymentState.Succeeded, status.Status);
        Assert.Equal(Now.AddMinutes(-1), status.PaidAtUtc);
        Assert.False(status.CanPay);
    }

    [Theory]
    [InlineData(-1, PaymentIntentProvider.VNPAY)]
    [InlineData(15, PaymentIntentProvider.Counter)]
    public async Task Expired_or_counter_request_cannot_be_paid_online(int minutesLeft, PaymentIntentProvider provider)
    {
        var store = new Store();
        store.Intent.ExpiresAtUtc = Now.AddMinutes(minutesLeft);
        store.Intent.Provider = provider;

        var status = await Service(store).GetAsync(store.Intent.Id, Payer, Ct);

        Assert.NotNull(status);
        Assert.False(status.CanPay);
    }

    [Theory]
    [InlineData("other user")]
    [InlineData("access revoked")]
    [InlineData("unknown request")]
    public async Task Anyone_else_gets_nothing_and_the_gateway_is_not_called(string reason)
    {
        var store = new Store();
        var identity = new Identity();
        var gateway = new FakeGateway();
        var userId = Payer;
        var paymentIntentId = store.Intent.Id;
        switch (reason)
        {
            case "other user": userId = Guid.NewGuid(); identity.Allowed.Add((userId, Patient)); break;
            case "access revoked": identity.Allowed.Clear(); break;
            default: paymentIntentId = Guid.NewGuid(); break;
        }

        var service = Service(store, identity, gateway);

        Assert.Null(await service.GetAsync(paymentIntentId, userId, Ct));
        Assert.Null(await service.StartCheckoutAsync(paymentIntentId, userId, "203.0.113.7", Ct));
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Checkout_returns_the_gateway_link_built_from_stored_values()
    {
        var store = new Store();
        var gateway = new FakeGateway();

        var checkout = await Service(store, gateway: gateway).StartCheckoutAsync(store.Intent.Id, Payer, "203.0.113.7", Ct);

        Assert.NotNull(checkout);
        Assert.Equal("http://gateway.test/pay", checkout.PaymentUrl);
        Assert.Equal(150_000m, checkout.Amount);
        var sent = Assert.Single(gateway.Requests);
        Assert.Equal("REF0001", sent.TransactionReference);
        Assert.Equal("203.0.113.7", sent.CustomerIp);
    }

    [Fact]
    public async Task Checkout_of_a_paid_request_is_a_conflict()
    {
        var store = new Store();
        store.Intent.Status = PaymentIntentStatus.Succeeded;
        store.Transaction.PaymentStatus = PaymentStatus.Success;
        var gateway = new FakeGateway();

        var error = await Assert.ThrowsAsync<BillingException>(() =>
            Service(store, gateway: gateway).StartCheckoutAsync(store.Intent.Id, Payer, null, Ct));

        Assert.Equal(BillingErrorCode.PaymentConflict, error.Code);
        Assert.Empty(gateway.Requests);
    }

    private static PatientPaymentService Service(Store store, Identity? identity = null, FakeGateway? gateway = null)
    {
        var clock = new FixedClock(Now);
        return new PatientPaymentService(store, new PaymentCheckoutService(store, gateway ?? new FakeGateway(), clock),
            identity ?? new Identity(), clock);
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeGateway : IPaymentGateway
    {
        public List<GatewayCheckoutRequest> Requests { get; } = [];

        public Task<GatewayCheckout> CreateCheckoutAsync(GatewayCheckoutRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new GatewayCheckout("http://gateway.test/pay"));
        }
    }

    /// <summary>Chỉ cài CanAccessPatientAsync; mặc định người trả có quyền đặt lịch trên hồ sơ bệnh nhân.</summary>
    private sealed class Identity : IIdentityPatientsModule
    {
        public HashSet<(Guid UserId, Guid PatientId)> Allowed { get; } = [(Payer, Patient)];

        public Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, AccessLevel level = AccessLevel.Booking, CancellationToken ct = default) =>
            Task.FromResult(level == AccessLevel.Booking && Allowed.Contains((userId, patientId)));

        private static InvalidOperationException Unused() => new("PatientPaymentService không dùng hàm này.");
        public Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default) => throw Unused();
        public Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default) => throw Unused();
        public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid roomId, DateOnly date, string shiftName, CancellationToken ct = default) => throw Unused();
    }

    /// <summary>Một yêu cầu thanh toán phí khám đang chờ trong bộ nhớ; chỉ cài các hàm đọc mà luồng bệnh nhân dùng.</summary>
    private sealed class Store : IPaymentStore
    {
        public PaymentIntent Intent { get; } = new()
        {
            Id = Guid.NewGuid(),
            IntentNumber = "PI-20261105-ABC",
            PayerAppUserId = Payer,
            PatientId = Patient,
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

        public bool HasActiveTransaction => false;

        public Task<(PaymentIntent Intent, PaymentTransaction Transaction)?> FindPaymentAsync(Guid paymentIntentId, CancellationToken ct) =>
            Task.FromResult<(PaymentIntent, PaymentTransaction)?>(paymentIntentId == Intent.Id ? (Intent, Transaction) : null);

        private static InvalidOperationException Unused() => new("Luồng bệnh nhân chỉ đọc, không dùng hàm này.");
        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => throw Unused();
        public Task<IReadOnlyList<BillingItem>> LockChargesAsync(IReadOnlyList<Guid> chargeIds, CancellationToken ct) => throw Unused();
        public Task LockBookingAttemptAsync(Guid bookingAttemptId, CancellationToken ct) => throw Unused();
        public Task<IReadOnlyList<OpenPayment>> FindOpenPaymentsAsync(IReadOnlyCollection<Guid> chargeIds, Guid? bookingAttemptId, DateTime nowUtc, CancellationToken ct) => throw Unused();
        public void Add(PaymentIntent intent, PaymentTransaction transaction, IReadOnlyList<PaymentAllocation> allocations) => throw Unused();
        public Task<PaymentTransaction?> LockTransactionAsync(string transactionReference, CancellationToken ct) => throw Unused();
        public Task<bool> IsProviderTransactionIdTakenAsync(string providerTransactionId, Guid exceptTransactionId, CancellationToken ct) => throw Unused();
        public Task<PaymentIntent> GetIntentAsync(Guid paymentIntentId, CancellationToken ct) => throw Unused();
        public Task<bool> HasEventAsync(PaymentEventProvider provider, string eventId, CancellationToken ct) => throw Unused();
        public void AddEvent(PaymentEvent paymentEvent) => throw Unused();
        public Task<IReadOnlyList<PaymentAllocation>> GetAllocationsAsync(Guid paymentTransactionId, CancellationToken ct) => throw Unused();
        public Task SaveChangesAsync(CancellationToken ct) => throw Unused();
        public Task EnqueueAsync(PaymentVerified integrationEvent, CancellationToken ct) => throw Unused();
    }
}
