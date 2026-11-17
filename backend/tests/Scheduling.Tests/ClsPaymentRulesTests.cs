using Billing.Contracts;
using Scheduling.Application.Queues;
using Xunit;

namespace Scheduling.Tests;

public sealed class ClsPaymentRulesTests
{
    [Theory]
    [InlineData(ChargeStatus.Pending, 0, 0, false)]
    [InlineData(ChargeStatus.PartiallyPaid, 50, 0, false)]
    [InlineData(ChargeStatus.Cancelled, 100, 0, false)]
    [InlineData(ChargeStatus.Waived, 100, 0, false)]
    [InlineData(ChargeStatus.Paid, 99, 0, false)]
    [InlineData(ChargeStatus.Paid, 100, 1, false)]
    [InlineData(ChargeStatus.Paid, 100, 0, true)]
    public void Only_the_exact_verified_charge_allows_execution(ChargeStatus status, int paid, int refunded, bool expected)
    {
        var encounter = Guid.NewGuid(); var item = Guid.NewGuid();
        var charge = new ChargeDto(Guid.NewGuid(), ChargeType.CLSService, Guid.NewGuid(), "Test", 100, 1, 100, paid, refunded,
            "VND", status, encounter, null, item);
        EncounterCostSummary Summary(params ChargeDto[] charges) => new(encounter, "VND", 100, 100, 0, 100, 0, 0, charges);
        Assert.Equal(expected, ClsPaymentRules.IsPaid(Summary(charge), encounter, item));
        Assert.False(ClsPaymentRules.IsPaid(Summary(charge with { ServiceOrderItemId = Guid.NewGuid() }), encounter, item));
        Assert.False(ClsPaymentRules.IsPaid(Summary(charge), Guid.NewGuid(), item));
        Assert.False(ClsPaymentRules.IsPaid(Summary(charge with { EncounterId = Guid.NewGuid() }), encounter, item));
        Assert.False(ClsPaymentRules.IsPaid(Summary(charge with { ChargeType = ChargeType.ConsultationFee }), encounter, item));
        Assert.False(ClsPaymentRules.IsPaid(Summary(charge, charge), encounter, item));
        Assert.False(ClsPaymentRules.IsPaid(Summary(), encounter, item));
    }
}
