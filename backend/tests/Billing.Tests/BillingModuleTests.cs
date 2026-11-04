using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Xunit;
using static Billing.Tests.PriceFixtures;

namespace Billing.Tests;

public class BillingModuleTests
{
    private static readonly Guid Doctor = Guid.NewGuid();
    private static readonly Guid Rank = Guid.NewGuid();
    private static readonly Guid Ultrasound = Guid.NewGuid();
    private static readonly Guid XRay = Guid.NewGuid();
    private static readonly Guid Encounter = Guid.NewGuid();

    private readonly FakeCatalog _catalog = new();
    private readonly FakeChargeStore _store = new();
    private readonly BillingModule _module;

    public BillingModuleTests()
    {
        var list = List("BG-2027", Jan1);
        _catalog.Items.AddRange([RankFee(list, Rank, 150_000m), ClsFee(list, Ultrasound, 200_000m), ClsFee(list, XRay, 120_000m)]);
        var clock = new FixedClock(Mid);
        _module = new BillingModule(_catalog, _store, new PaymentService(new UnusedPaymentStore(), clock), clock);
    }

    [Fact]
    public async Task Exam_charge_snapshots_current_price()
    {
        var charge = await _module.CreateExamChargeAsync(new CreateExamCharge(Doctor, Rank, "Phí khám - BS giả lập", Mid), Ct);

        Assert.Equal(ChargeType.ConsultationFee, charge.ChargeType);
        Assert.Equal(150_000m, charge.UnitPrice);
        Assert.Equal(1, charge.Quantity);
        Assert.Equal(150_000m, charge.TotalAmount);
        Assert.Equal(ChargeStatus.Pending, charge.Status);
        Assert.Single(_store.Items);
        Assert.Equal(1, _store.Saves);
    }

    [Fact]
    public async Task Cls_charges_use_billing_prices_and_quantity()
    {
        var soi1 = Guid.NewGuid();
        var soi2 = Guid.NewGuid();

        var result = await _module.CreateClsChargesAsync(new CreateClsCharges(Encounter, Mid,
        [
            new ClsChargeLine(soi1, Ultrasound, "Siêu âm ổ bụng", 1),
            new ClsChargeLine(soi2, XRay, "X-quang ngực", 2),
        ]), Ct);

        Assert.Equal([200_000m, 240_000m], result.Select(r => r.TotalAmount));
        Assert.All(result, r => Assert.Equal(Encounter, r.EncounterId));
        Assert.Equal([soi1, soi2], result.Select(r => r.ServiceOrderItemId!.Value));
    }

    [Fact]
    public async Task Repeating_cls_request_returns_existing_charges_without_duplicates()
    {
        var command = new CreateClsCharges(Encounter, Mid, [new ClsChargeLine(Guid.NewGuid(), Ultrasound, "Siêu âm ổ bụng", 1)]);

        var first = await _module.CreateClsChargesAsync(command, Ct);
        var second = await _module.CreateClsChargesAsync(command, Ct);

        Assert.Single(_store.Items);
        Assert.Equal(first[0].ChargeId, second[0].ChargeId);
        Assert.Equal(1, _store.Saves);
    }

    [Fact]
    public async Task Price_change_after_charge_does_not_change_the_charge()
    {
        var soi = Guid.NewGuid();
        await _module.CreateClsChargesAsync(new CreateClsCharges(Encounter, Mid, [new ClsChargeLine(soi, Ultrasound, "Siêu âm ổ bụng", 1)]), Ct);

        _catalog.Items.Clear();
        _catalog.Items.Add(ClsFee(List("BG-TANG-GIA", Jan1), Ultrasound, 250_000m));

        var summary = await _module.GetEncounterCostSummaryAsync(Encounter, Ct);
        Assert.Equal(200_000m, summary.TotalPayable);
    }

    [Fact]
    public async Task Missing_cls_price_fails_whole_request()
    {
        var command = new CreateClsCharges(Encounter, Mid,
        [
            new ClsChargeLine(Guid.NewGuid(), Ultrasound, "Siêu âm ổ bụng", 1),
            new ClsChargeLine(Guid.NewGuid(), Guid.NewGuid(), "Dịch vụ chưa có giá", 1),
        ]);

        var error = await Assert.ThrowsAsync<BillingException>(() => _module.CreateClsChargesAsync(command, Ct));
        Assert.Equal(BillingErrorCode.PriceNotConfigured, error.Code);
        Assert.Empty(_store.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_quantity_is_rejected(int quantity)
    {
        var command = new CreateClsCharges(Encounter, Mid, [new ClsChargeLine(Guid.NewGuid(), Ultrasound, "Siêu âm", quantity)]);
        var error = await Assert.ThrowsAsync<BillingException>(() => _module.CreateClsChargesAsync(command, Ct));
        Assert.Equal(BillingErrorCode.InvalidRequest, error.Code);
    }

    [Fact]
    public async Task Charge_of_another_encounter_is_not_reused()
    {
        var soi = Guid.NewGuid();
        await _module.CreateClsChargesAsync(new CreateClsCharges(Encounter, Mid, [new ClsChargeLine(soi, Ultrasound, "Siêu âm", 1)]), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() =>
            _module.CreateClsChargesAsync(new CreateClsCharges(Guid.NewGuid(), Mid, [new ClsChargeLine(soi, Ultrasound, "Siêu âm", 1)]), Ct));
        Assert.Equal(BillingErrorCode.InvalidRequest, error.Code);
    }

    [Fact]
    public async Task Existing_duplicate_active_charges_are_reported_not_guessed()
    {
        var soi = Guid.NewGuid();
        for (var i = 0; i < 2; i++)
        {
            var dup = BillingItem.CreatePending(Guid.NewGuid(), ItemType.CLSService, "Siêu âm", 200_000m, 1, Mid, encounterId: Encounter, serviceOrderItemId: soi);
            dup.Id = Guid.NewGuid();
            _store.Items.Add(dup);
        }

        var error = await Assert.ThrowsAsync<BillingException>(() =>
            _module.CreateClsChargesAsync(new CreateClsCharges(Encounter, Mid, [new ClsChargeLine(soi, Ultrasound, "Siêu âm", 1)]), Ct));

        Assert.Equal(BillingErrorCode.DuplicateCharge, error.Code);
        Assert.Contains(soi.ToString(), error.Message);
        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public async Task Cancelled_charge_does_not_block_a_new_charge()
    {
        var soi = Guid.NewGuid();
        var cancelled = BillingItem.CreatePending(Guid.NewGuid(), ItemType.CLSService, "Siêu âm", 200_000m, 1, Mid, encounterId: Encounter, serviceOrderItemId: soi);
        cancelled.Id = Guid.NewGuid();
        cancelled.Status = BillingItemStatus.Cancelled;
        _store.Items.Add(cancelled);

        var result = await _module.CreateClsChargesAsync(new CreateClsCharges(Encounter, Mid, [new ClsChargeLine(soi, Ultrasound, "Siêu âm", 1)]), Ct);

        Assert.NotEqual(cancelled.Id, result[0].ChargeId);
        Assert.Equal(ChargeStatus.Pending, result[0].Status);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeCatalog : IPriceCatalog
    {
        public List<PriceCandidate> Items { get; } = [];

        public Task<IReadOnlyList<PriceCandidate>> GetConsultationCandidatesAsync(Guid doctorId, Guid doctorRankId, DateTime atUtc, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PriceCandidate>>(Items);

        public Task<IReadOnlyList<PriceCandidate>> GetClsCandidatesAsync(IReadOnlyCollection<Guid> medicalServiceIds, DateTime atUtc, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PriceCandidate>>(Items);
    }

    private sealed class FakeChargeStore : IChargeStore
    {
        private readonly List<BillingItem> _pending = [];
        public List<BillingItem> Items { get; } = [];
        public int Saves { get; private set; }

        public Task<IReadOnlyList<BillingItem>> GetByServiceOrderItemsAsync(IReadOnlyCollection<Guid> serviceOrderItemIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<BillingItem>>(Items.Where(i => i.ServiceOrderItemId is { } id && serviceOrderItemIds.Contains(id)).ToList());

        public Task<IReadOnlyList<BillingItem>> GetByEncounterAsync(Guid encounterId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<BillingItem>>(Items.Where(i => i.EncounterId == encounterId).ToList());

        public void Add(BillingItem item)
        {
            item.Id = Guid.NewGuid();
            _pending.Add(item);
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            Items.AddRange(_pending);
            _pending.Clear();
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
