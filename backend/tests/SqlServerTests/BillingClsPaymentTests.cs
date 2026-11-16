using Billing.Application;
using Billing.Contracts;
using Billing.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Thanh toán phí CLS theo đợt chỉ định trên SQL Server thật: khoản thu tạo qua BillingModule như Clinical sẽ gọi,
// rồi web bệnh nhân xem đợt và tạo yêu cầu thanh toán. Dữ liệu giả lập, rollback sau mỗi test.
// Không gọi cổng ở đây: PaymentCheckoutService chặn gọi cổng khi còn transaction (transaction của test luôn mở).
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingClsPaymentTests
{
    private static readonly Guid Payer = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();

    [Fact]
    public async Task Patient_sees_today_batches_grouped_by_service_order()
    {
        await using var db = await OpenAsync();
        var prices = await PricesAsync(db);
        var encounter = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await ChargesAsync(db, encounter, Patient, first, (prices.Ultrasound, 1), (prices.XRay, 2));
        await ChargesAsync(db, encounter, Patient, second, (prices.Ultrasound, 1));
        await ChargesAsync(db, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (prices.XRay, 1));

        var batches = await Service(db).ListTodayAsync(Patient, Payer, Ct);

        Assert.NotNull(batches);
        Assert.Equal([(first, 440_000m), (second, 200_000m)],
            batches.Select(b => (b.ServiceOrderId, b.Outstanding)).OrderByDescending(b => b.Outstanding));
        Assert.All(batches, b => Assert.Equal((ClsBatchStatus.PendingPayment, encounter), (b.Status, b.EncounterId)));
        Assert.Null(await Service(db).ListTodayAsync(Patient, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Batch_payment_covers_what_is_left_and_is_reused_until_it_expires()
    {
        await using var db = await OpenAsync();
        var prices = await PricesAsync(db);
        var encounter = Guid.NewGuid();
        var order = Guid.NewGuid();
        await ChargesAsync(db, encounter, Patient, order, (prices.Ultrasound, 1), (prices.XRay, 2));
        var before = DateTime.UtcNow;

        var first = await Service(db).CreatePaymentAsync(order, Payer, Ct);
        var again = await Service(db).CreatePaymentAsync(order, Payer, Ct);

        Assert.Equal(ClsPaymentOutcome.Created, first.Outcome);
        var payment = first.Payment!;
        Assert.Equal(payment.PaymentIntentId, again.Payment!.PaymentIntentId);
        Assert.Equal(440_000m, payment.Amount);
        Assert.Equal(PaymentPurpose.CLSOrderBatch, payment.Purpose);
        Assert.Equal(2, payment.ChargeIds.Count);
        Assert.InRange(payment.ExpiresAtUtc, before, before.AddMinutes(16));
        Assert.True(payment.ExpiresAtUtc <= ClsBatches.PayByUtc(before));
        Assert.Equal([$"CLSOrderBatch|{encounter}|{Patient}|{Payer}".ToUpperInvariant()], await db.QueryAsync($"""
            SELECT UPPER(CONCAT(PaymentType, '|', EncounterId, '|', PatientId, '|', PayerAppUserId))
            FROM billing.PaymentIntent WHERE Id = '{payment.PaymentIntentId}'
            """));
    }

    [Fact]
    public async Task Paid_batch_has_nothing_to_pay()
    {
        await using var db = await OpenAsync();
        var prices = await PricesAsync(db);
        var order = Guid.NewGuid();
        await ChargesAsync(db, Guid.NewGuid(), Patient, order, (prices.Ultrasound, 1));
        var payment = (await Service(db).CreatePaymentAsync(order, Payer, Ct)).Payment!;

        await Payments(db).ConfirmVerifiedAsync(new VerifiedPayment(payment.TransactionReference, "VNP-CLS-1", "VNP-CLS-1",
            payment.Amount, "VND", DateTime.UtcNow, "{}"), Ct);

        Assert.Equal(ClsPaymentOutcome.NothingToPay, (await Service(db).CreatePaymentAsync(order, Payer, Ct)).Outcome);
        var batch = Assert.Single((await Service(db).ListTodayAsync(Patient, Payer, Ct))!);
        Assert.Equal((ClsBatchStatus.Paid, 200_000m, 0m), (batch.Status, batch.PaidAmount, batch.Outstanding));
    }

    [Fact]
    public async Task Unknown_batch_or_user_without_access_is_not_found()
    {
        await using var db = await OpenAsync();
        var prices = await PricesAsync(db);
        var order = Guid.NewGuid();
        await ChargesAsync(db, Guid.NewGuid(), Patient, order, (prices.Ultrasound, 1));

        Assert.Equal(ClsPaymentOutcome.NotFound, (await Service(db).CreatePaymentAsync(Guid.NewGuid(), Payer, Ct)).Outcome);
        Assert.Equal(ClsPaymentOutcome.NotFound, (await Service(db).CreatePaymentAsync(order, Guid.NewGuid(), Ct)).Outcome);
        Assert.Equal(["0"], await db.QueryAsync($"SELECT COUNT(*) FROM billing.PaymentIntent WHERE PatientId = '{Patient}'"));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Prices(Guid Ultrasound, Guid XRay);

    private static async Task<Prices> PricesAsync(TestDatabase db)
    {
        var list = await db.InsertAsync("billing.PriceList", new
        {
            PriceListCode = "T-" + Guid.NewGuid().ToString("N")[..10], Currency = "VND", IsActive = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
        });
        var ultrasound = Guid.NewGuid();
        var xray = Guid.NewGuid();
        await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = "CLSService", MedicalServiceId = ultrasound, UnitPrice = 200_000m, IsActive = true });
        await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = "CLSService", MedicalServiceId = xray, UnitPrice = 120_000m, IsActive = true });
        return new Prices(ultrasound, xray);
    }

    // Như Clinical gọi khi bác sĩ chỉ định: một đợt, mỗi dịch vụ một ServiceOrderItem.
    private static Task<IReadOnlyList<ChargeDto>> ChargesAsync(TestDatabase db, Guid encounter, Guid patient, Guid order,
        params (Guid Service, int Quantity)[] lines) =>
        Module(db).CreateClsChargesAsync(new CreateClsCharges(encounter, patient, order, DateTime.UtcNow,
            lines.Select(l => new ClsChargeLine(Guid.NewGuid(), l.Service, "Dịch vụ CLS giả lập", l.Quantity)).ToList()), Ct);

    // Mỗi lần gọi một cặp DbContext mới (Billing + Shared) trên cùng kết nối và transaction của test.
    private static (BillingDbContext Billing, EfPaymentStore Store) Contexts(TestDatabase db)
    {
        var billing = db.CreateContext<BillingDbContext>(options => new BillingDbContext(options));
        var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        return (billing, new EfPaymentStore(billing, new OutboxWriter(shared, TimeProvider.System)));
    }

    private static PaymentService Payments(TestDatabase db) => new(Contexts(db).Store, TimeProvider.System);

    private static BillingModule Module(TestDatabase db)
    {
        var (billing, store) = Contexts(db);
        return new BillingModule(new EfPriceCatalog(billing), new EfChargeStore(billing), new PaymentService(store, TimeProvider.System), TimeProvider.System);
    }

    private static PatientClsPaymentService Service(TestDatabase db)
    {
        var (billing, store) = Contexts(db);
        var identity = new Identity();
        var checkout = new PaymentCheckoutService(store, new NoGateway(), TimeProvider.System);
        return new PatientClsPaymentService(new EfChargeStore(billing), new PaymentService(store, TimeProvider.System),
            new PatientPaymentService(store, checkout, identity, TimeProvider.System), identity, TimeProvider.System);
    }

    private sealed class NoGateway : IPaymentGateway
    {
        public Task<GatewayCheckout> CreateCheckoutAsync(GatewayCheckoutRequest request, CancellationToken ct) =>
            throw new InvalidOperationException("Test SQL không gọi cổng thanh toán.");
    }

    /// <summary>Chỉ cài CanAccessPatientAsync: Payer có quyền đặt lịch trên hồ sơ Patient.</summary>
    private sealed class Identity : IIdentityPatientsModule
    {
        public Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, AccessLevel level = AccessLevel.Booking, CancellationToken ct = default) =>
            Task.FromResult(level == AccessLevel.Booking && userId == Payer && patientId == Patient);

        private static InvalidOperationException Unused() => new("Thanh toán CLS không dùng hàm này.");
        public Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default) => throw Unused();
        public Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default) => throw Unused();
        public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid roomId, DateOnly date, string shiftName, CancellationToken ct = default) => throw Unused();
    }
}
