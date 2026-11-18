using Clinical.Contracts;

namespace Scheduling.Application.Queues;

public enum ReturnTicketReadiness { Ready, NotReady, Superseded }

public static class ReturnTicketRules
{
    public static ReturnTicketReadiness Check(Guid encounterId, Guid orderId, IReadOnlyList<ServiceOrderDto> orders)
    {
        var order = orders.SingleOrDefault(o => o.Id == orderId && o.EncounterId == encounterId);
        if (order is null || order.OrderBatchStatus is not ("Confirmed" or "Completed")) return ReturnTicketReadiness.NotReady;
        if (orders.Any(o => o.EncounterId == encounterId && o.OrderRoundNumber > order.OrderRoundNumber
            && o.OrderBatchStatus is "Confirmed" or "Completed")) return ReturnTicketReadiness.Superseded;
        // IsMandatory: mục phải có kết quả mới kích hoạt vé quay lại (ERD). Đợt không có mục bắt buộc thì cần mọi mục chưa hủy,
        // nếu không đợt chỉ gồm mục tùy chọn sẽ không bao giờ có vé quay lại.
        var mandatory = order.Items.Where(i => i.IsMandatory).ToList();
        if (mandatory.Count == 0)
            mandatory = order.Items.Where(i => i.ItemStatus != "Cancelled").ToList();
        // shortcut: cancelled/waived items need Clinical's authorized-decision readiness contract before they can satisfy this gate.
        return mandatory.Count > 0 && mandatory.All(i => i.ServiceOrderId == orderId && i.ItemStatus is "ServiceFinished" or "Completed"
            && i.Result is { ResultStatus: "Published", PublishedAtUtc: not null } r && r.ServiceOrderItemId == i.Id)
            ? ReturnTicketReadiness.Ready : ReturnTicketReadiness.NotReady;
    }
}
