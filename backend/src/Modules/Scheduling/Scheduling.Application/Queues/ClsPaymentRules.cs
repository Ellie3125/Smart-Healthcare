using Billing.Contracts;

namespace Scheduling.Application.Queues;

public static class ClsPaymentRules
{
    public static bool IsPaid(EncounterCostSummary summary, Guid encounterId, Guid itemId)
    {
        if (summary.EncounterId != encounterId || itemId == Guid.Empty) return false;
        var charges = summary.Charges.Where(c => c.ServiceOrderItemId == itemId && c.Status != ChargeStatus.Cancelled).ToList();
        return charges is [var charge] && charge.EncounterId == encounterId && charge.ChargeType == ChargeType.CLSService
            && charge.Status == ChargeStatus.Paid && charge.TotalAmount >= 0 && charge.PaidAmount >= charge.TotalAmount
            && charge.RefundedAmount == 0;
    }
}
