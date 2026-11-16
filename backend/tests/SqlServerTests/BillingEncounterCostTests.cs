using Billing.Application;
using Billing.Contracts;
using Billing.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using Scheduling.Contracts;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Gắn phí khám đã thu lúc đặt lịch với lịch hẹn (AppointmentConfirmed) rồi với lượt khám (check-in), và tổng hợp chi phí lượt
// khám cho web bệnh nhân. SQL Server thật, dữ liệu giả lập, rollback sau mỗi test.
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingEncounterCostTests
{
    private static readonly Guid Payer = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();

    [Fact]
    public async Task Paid_exam_fee_follows_appointment_then_encounter_and_shows_in_cost_summary()
    {
        await using var db = await OpenAsync();
        var (attempt, examCharge) = await PaidExamFeeAsync(db, 150_000m);
        var appointment = Guid.NewGuid();
        var encounter = Guid.NewGuid();

        Assert.Equal(1, await Linker(db).AttachAppointmentAsync(Confirmed(appointment, attempt), Ct));
        Assert.Equal(1, await Linker(db).LinkEncounterAsync(new LinkEncounter(appointment, encounter, Patient), Ct));
        await ClsChargeAsync(db, encounter, 200_000m);

        var summary = await Costs(db).GetAsync(encounter, Payer, Ct);

        Assert.NotNull(summary);
        Assert.Equal(350_000m, summary.TotalPayable);
        Assert.Equal(150_000m, summary.TotalPaid);
        Assert.Equal(200_000m, summary.Outstanding);
        Assert.Contains(summary.Charges, c => c.ChargeId == examCharge && c.ChargeType == ChargeType.ConsultationFee && c.AppointmentId == appointment);
    }

    [Fact]
    public async Task Linking_is_repeatable_and_late_or_stale_events_do_not_overwrite()
    {
        await using var db = await OpenAsync();
        var (attempt, examCharge) = await PaidExamFeeAsync(db, 150_000m);
        var first = Guid.NewGuid();
        var moved = Guid.NewGuid();

        await Linker(db).AttachAppointmentAsync(Confirmed(first, attempt), Ct);
        // Đổi lịch: phí chuyển sang lịch mới.
        Assert.Equal(1, await Linker(db).AttachAppointmentAsync(Confirmed(moved, attempt, rescheduledFrom: first), Ct));
        // Sự kiện xác nhận lịch đầu tiên giao lại muộn: không kéo phí về lịch cũ.
        Assert.Equal(0, await Linker(db).AttachAppointmentAsync(Confirmed(first, attempt), Ct));
        Assert.Equal([moved.ToString()], await db.QueryAsync($"SELECT LOWER(CONVERT(varchar(36), AppointmentId)) FROM billing.BillingItem WHERE Id = '{examCharge}'"));

        var encounter = Guid.NewGuid();
        Assert.Equal(1, await Linker(db).LinkEncounterAsync(new LinkEncounter(moved, encounter, Patient), Ct));
        Assert.Equal(1, await Linker(db).LinkEncounterAsync(new LinkEncounter(moved, encounter, Patient), Ct));
        var other = await Assert.ThrowsAsync<BillingException>(() =>
            Linker(db).LinkEncounterAsync(new LinkEncounter(moved, Guid.NewGuid(), Patient), Ct));
        Assert.Equal(BillingErrorCode.InvalidRequest, other.Code);
        Assert.Equal(0, await Linker(db).LinkEncounterAsync(new LinkEncounter(first, Guid.NewGuid(), Patient), Ct));
    }

    [Fact]
    public async Task Unpaid_exam_fee_is_not_attached()
    {
        await using var db = await OpenAsync();
        var attempt = Guid.NewGuid();
        var charge = await ExamChargeAsync(db, 150_000m);
        await Payments(db).CreateAsync(Exam(attempt, charge), Ct);

        Assert.Equal(0, await Linker(db).AttachAppointmentAsync(Confirmed(Guid.NewGuid(), attempt), Ct));
    }

    [Fact]
    public async Task Cost_summary_is_hidden_from_other_users_and_unknown_encounters()
    {
        await using var db = await OpenAsync();
        var encounter = Guid.NewGuid();
        await ClsChargeAsync(db, encounter, 200_000m);

        Assert.NotNull(await Costs(db).GetAsync(encounter, Payer, Ct));
        Assert.Null(await Costs(db).GetAsync(encounter, Guid.NewGuid(), Ct));
        Assert.Null(await Costs(db).GetAsync(Guid.NewGuid(), Payer, Ct));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AppointmentConfirmed Confirmed(Guid appointment, Guid attempt, Guid? rescheduledFrom = null) =>
        new(Guid.NewGuid(), appointment, attempt, Patient, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "LH-TEST",
            rescheduledFrom, DateTime.UtcNow, null);

    private static CreatePayment Exam(Guid attempt, Guid charge) =>
        new(PaymentPurpose.InitialConsultation, Patient, Payer, attempt, null, [charge], PaymentProvider.VNPAY, DateTime.UtcNow.AddMinutes(5));

    // Phí khám đã thu qua VNPAY: tạo khoản, yêu cầu thanh toán, rồi ghi nhận IPN đã xác minh.
    private static async Task<(Guid Attempt, Guid Charge)> PaidExamFeeAsync(TestDatabase db, decimal price)
    {
        var attempt = Guid.NewGuid();
        var charge = await ExamChargeAsync(db, price);
        var payment = await Payments(db).CreateAsync(Exam(attempt, charge), Ct);
        var txn = "VNP-" + Guid.NewGuid().ToString("N")[..12];
        await Payments(db).ConfirmVerifiedAsync(new VerifiedPayment(payment.TransactionReference, txn, txn, payment.Amount, "VND", DateTime.UtcNow, "{}"), Ct);
        return (attempt, charge);
    }

    private static async Task<Guid> PriceItemAsync(TestDatabase db, string type, decimal price)
    {
        var list = await db.InsertAsync("billing.PriceList", new
        {
            PriceListCode = "T-" + Guid.NewGuid().ToString("N")[..10], Currency = "VND", IsActive = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
        });
        return type == "ConsultationFee"
            ? await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = type, DoctorRankId = Guid.NewGuid(), UnitPrice = price, IsActive = true })
            : await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = type, MedicalServiceId = Guid.NewGuid(), UnitPrice = price, IsActive = true });
    }

    private static async Task<Guid> ExamChargeAsync(TestDatabase db, decimal price) =>
        await db.InsertAsync("billing.BillingItem", new
        {
            PriceListItemId = await PriceItemAsync(db, "ConsultationFee", price), ItemType = "ConsultationFee", ItemNameSnapshot = "Phí khám giả lập",
            UnitPrice = price, Quantity = 1, TotalAmount = price, Status = "Pending", CreatedAtUtc = DateTime.UtcNow,
        });

    private static async Task<Guid> ClsChargeAsync(TestDatabase db, Guid encounter, decimal price) =>
        await db.InsertAsync("billing.BillingItem", new
        {
            EncounterId = encounter, ServiceOrderItemId = Guid.NewGuid(), ServiceOrderId = Guid.NewGuid(), PatientId = Patient,
            PriceListItemId = await PriceItemAsync(db, "CLSService", price), ItemType = "CLSService", ItemNameSnapshot = "Dịch vụ CLS giả lập",
            UnitPrice = price, Quantity = 1, TotalAmount = price, Status = "Pending", CreatedAtUtc = DateTime.UtcNow,
        });

    private static (BillingDbContext Billing, EfPaymentStore Store) Contexts(TestDatabase db)
    {
        var billing = db.CreateContext<BillingDbContext>(options => new BillingDbContext(options));
        var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        return (billing, new EfPaymentStore(billing, new OutboxWriter(shared, TimeProvider.System)));
    }

    private static PaymentService Payments(TestDatabase db) => new(Contexts(db).Store, TimeProvider.System);

    private static ExamChargeLinker Linker(TestDatabase db)
    {
        var (billing, store) = Contexts(db);
        return new ExamChargeLinker(new EfChargeStore(billing), store);
    }

    private static PatientCostSummaryService Costs(TestDatabase db) => new(new EfChargeStore(Contexts(db).Billing), new Identity());

    /// <summary>Chỉ cài CanAccessPatientAsync: Payer có quyền trên hồ sơ Patient.</summary>
    private sealed class Identity : IIdentityPatientsModule
    {
        public Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, AccessLevel level = AccessLevel.Booking, CancellationToken ct = default) =>
            Task.FromResult(userId == Payer && patientId == Patient);

        private static InvalidOperationException Unused() => new("Tổng hợp chi phí không dùng hàm này.");
        public Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default) => throw Unused();
        public Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default) => throw Unused();
        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default) => throw Unused();
        public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid roomId, DateOnly date, string shiftName, CancellationToken ct = default) => throw Unused();
    }
}
