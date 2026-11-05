using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Xunit;

namespace Billing.Tests;

public class PaymentRulesTests
{
    private static readonly DateTime Now = new(2027, 1, 15, 2, 0, 0, DateTimeKind.Utc);

    private static BillingItem Charge(decimal total = 200_000m) =>
        BillingItem.CreatePending(Guid.NewGuid(), ItemType.CLSService, "Siêu âm", total, 1, Now, encounterId: Guid.NewGuid());

    [Fact]
    public void Full_payment_marks_charge_paid_and_leaves_nothing_outstanding()
    {
        var charge = Charge();
        Assert.Equal(200_000m, charge.Outstanding);

        charge.ApplyPayment(200_000m);

        Assert.Equal(BillingItemStatus.Paid, charge.Status);
        Assert.Equal(0m, charge.Outstanding);
    }

    [Fact]
    public void Partial_payment_marks_charge_partially_paid()
    {
        var charge = Charge();
        charge.ApplyPayment(50_000m);

        Assert.Equal(BillingItemStatus.PartiallyPaid, charge.Status);
        Assert.Equal(150_000m, charge.Outstanding);
    }

    [Fact]
    public void Cancelled_charge_keeps_its_status_and_owes_nothing_but_still_records_money()
    {
        var charge = Charge();
        charge.Status = BillingItemStatus.Cancelled;
        Assert.Equal(0m, charge.Outstanding);

        charge.ApplyPayment(200_000m);

        Assert.Equal(BillingItemStatus.Cancelled, charge.Status);
        Assert.Equal(200_000m, charge.PaidAmount);
    }

    [Fact]
    public void Non_positive_payment_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Charge().ApplyPayment(0m));

    public static TheoryData<string, CreatePayment> InvalidRequests
    {
        get
        {
            var later = Now.AddMinutes(5);
            Guid[] one = [Guid.NewGuid()];
            return new()
            {
                { "phí khám thiếu BookingAttemptId", new(PaymentPurpose.InitialConsultation, Guid.NewGuid(), Guid.NewGuid(), null, null, one, PaymentProvider.VNPAY, later) },
                { "phí khám mang EncounterId", new(PaymentPurpose.InitialConsultation, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), one, PaymentProvider.VNPAY, later) },
                { "phí CLS thiếu EncounterId", new(PaymentPurpose.CLSOrderBatch, Guid.NewGuid(), Guid.NewGuid(), null, null, one, PaymentProvider.MoMo, later) },
                { "phí CLS mang BookingAttemptId", new(PaymentPurpose.CLSOrderBatch, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), one, PaymentProvider.MoMo, later) },
                { "không có khoản", new(PaymentPurpose.InitialConsultation, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], PaymentProvider.VNPAY, later) },
                { "khoản bị trùng", new(PaymentPurpose.InitialConsultation, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [one[0], one[0]], PaymentProvider.VNPAY, later) },
                { "hạn đã qua", new(PaymentPurpose.InitialConsultation, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, one, PaymentProvider.VNPAY, Now) },
                { "hạn không phải UTC", new(PaymentPurpose.InitialConsultation, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, one, PaymentProvider.VNPAY, DateTime.SpecifyKind(later, DateTimeKind.Local)) },
                { "thiếu PatientId", new(PaymentPurpose.InitialConsultation, Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), null, one, PaymentProvider.VNPAY, later) },
            };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Invalid_payment_request_is_rejected_before_any_database_work(string reason, CreatePayment command)
    {
        var service = new PaymentService(new UnusedPaymentStore(), new FixedClock(Now));

        var error = await Assert.ThrowsAsync<BillingException>(() => service.CreateAsync(command, TestContext.Current.CancellationToken));

        Assert.True(error.Code == BillingErrorCode.InvalidRequest, reason);
    }

    [Theory]
    [InlineData("", "VNP123")]
    [InlineData("REF", "")]
    public async Task Verified_payment_without_references_is_rejected(string reference, string providerId)
    {
        var service = new PaymentService(new UnusedPaymentStore(), new FixedClock(Now));

        var error = await Assert.ThrowsAsync<BillingException>(() => service.ConfirmVerifiedAsync(
            new VerifiedPayment(reference, providerId, 150_000m, "VND", Now, null), TestContext.Current.CancellationToken));

        Assert.Equal(BillingErrorCode.InvalidRequest, error.Code);
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
