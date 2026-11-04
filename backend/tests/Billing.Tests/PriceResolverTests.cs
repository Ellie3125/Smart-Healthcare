using Billing.Application;
using Billing.Contracts;
using Xunit;
using static Billing.Tests.PriceFixtures;

namespace Billing.Tests;

public class PriceResolverTests
{
    private static readonly Guid Doctor = Guid.NewGuid();
    private static readonly Guid Rank = Guid.NewGuid();
    private static readonly Guid OtherRank = Guid.NewGuid();
    private static readonly Guid Ultrasound = Guid.NewGuid();

    [Fact]
    public void Exam_fee_uses_rank_price_when_doctor_has_no_override()
    {
        var list = List("BG-2027", Jan1);
        var rank = RankFee(list, Rank, 150_000m);

        var quote = PriceResolver.ResolveExamFee([rank, RankFee(list, OtherRank, 300_000m)], Doctor, Rank, Mid);

        Assert.Equal(rank.Item.Id, quote.PriceListItemId);
        Assert.Equal(150_000m, quote.UnitPrice);
        Assert.Equal(PriceBasis.DoctorRank, quote.Basis);
        Assert.Equal(ChargeType.ConsultationFee, quote.ChargeType);
        Assert.Equal("VND", quote.Currency);
    }

    [Fact]
    public void Doctor_override_wins_over_rank_price()
    {
        var list = List("BG-2027", Jan1);

        var quote = PriceResolver.ResolveExamFee([RankFee(list, Rank, 150_000m), DoctorFee(list, Doctor, 250_000m)], Doctor, Rank, Mid);

        Assert.Equal(250_000m, quote.UnitPrice);
        Assert.Equal(PriceBasis.Doctor, quote.Basis);
    }

    [Fact]
    public void Price_list_is_effective_from_inclusive_to_exclusive()
    {
        var january = List("BG-01", Jan1, Feb1);
        var february = List("BG-02", Feb1);
        var candidates = new[] { RankFee(january, Rank, 150_000m), RankFee(february, Rank, 180_000m) };

        Assert.Equal(150_000m, PriceResolver.ResolveExamFee(candidates, Doctor, Rank, Jan1).UnitPrice);
        Assert.Equal(150_000m, PriceResolver.ResolveExamFee(candidates, Doctor, Rank, Feb1.AddTicks(-1)).UnitPrice);
        Assert.Equal(180_000m, PriceResolver.ResolveExamFee(candidates, Doctor, Rank, Feb1).UnitPrice);
        AssertError(BillingErrorCode.PriceNotConfigured, () => PriceResolver.ResolveExamFee(candidates, Doctor, Rank, Jan1.AddTicks(-1)));
    }

    [Fact]
    public void Inactive_list_inactive_item_and_non_VND_list_are_ignored()
    {
        var inactiveList = List("OFF", Jan1, active: false);
        var usd = List("USD", Jan1, currency: "USD");
        var active = List("ON", Jan1);

        var candidates = new[]
        {
            RankFee(inactiveList, Rank, 1m),
            RankFee(usd, Rank, 2m),
            RankFee(active, Rank, 3m, active: false),
        };

        AssertError(BillingErrorCode.PriceNotConfigured, () => PriceResolver.ResolveExamFee(candidates, Doctor, Rank, Mid));
    }

    [Fact]
    public void Two_effective_prices_for_the_same_basis_are_rejected_not_guessed()
    {
        var a = List("BG-A", Jan1);
        var b = List("BG-B", Jan1);

        var error = AssertError(BillingErrorCode.AmbiguousPrice,
            () => PriceResolver.ResolveExamFee([RankFee(a, Rank, 150_000m), RankFee(b, Rank, 160_000m)], Doctor, Rank, Mid));
        Assert.Contains("BG-A", error.Message);
        Assert.Contains("BG-B", error.Message);
    }

    [Fact]
    public void Cls_fee_is_resolved_by_medical_service()
    {
        var list = List("BG-2027", Jan1);

        var quote = PriceResolver.ResolveClsFee([ClsFee(list, Ultrasound, 200_000m), ClsFee(list, Guid.NewGuid(), 90_000m)], Ultrasound, Mid);

        Assert.Equal(200_000m, quote.UnitPrice);
        Assert.Equal(Ultrasound, quote.MedicalServiceId);
        Assert.Equal(PriceBasis.MedicalService, quote.Basis);
    }

    [Fact]
    public void Cls_price_item_is_not_used_as_exam_fee()
    {
        var list = List("BG-2027", Jan1);
        AssertError(BillingErrorCode.PriceNotConfigured, () => PriceResolver.ResolveExamFee([ClsFee(list, Ultrasound, 200_000m)], Doctor, Rank, Mid));
    }

    [Fact]
    public void Non_utc_time_is_rejected()
    {
        var local = new DateTime(2027, 1, 15, 9, 0, 0, DateTimeKind.Local);
        AssertError(BillingErrorCode.InvalidRequest, () => PriceResolver.ResolveClsFee([], Ultrasound, local));
    }

    internal static BillingException AssertError(BillingErrorCode code, Action action)
    {
        var error = Assert.Throws<BillingException>(action);
        Assert.Equal(code, error.Code);
        return error;
    }
}
