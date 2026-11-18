using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Clinical.Application.Results;

public sealed record ClsItemContext(Encounter Encounter, ServiceOrder Order, ServiceOrderItem Item);
public interface IClsLifecycleStore
{
    void RequireTransaction();
    Task<ClsItemContext?> LockAsync(Guid itemId, CancellationToken ct);
    Task SaveAsync(ClsItemContext context, bool finish, ClsServiceCommand command, DateTime nowUtc, CancellationToken ct);
}

public sealed class ClsLifecycleService(IClsLifecycleStore store, IClsTicketReader tickets, IDbClock clock)
{
    public async Task TransitionAsync(ClsServiceCommand command, bool start, CancellationToken ct)
    {
        if (command.ServiceOrderItemId == Guid.Empty || command.RoomId == Guid.Empty || command.PerformedByUserId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "Lệnh thực hiện CLS không hợp lệ.");
        store.RequireTransaction();
        var ticket = await tickets.GetByItemAsync(command.ServiceOrderItemId, ct);
        if (ticket is null) throw new ClinicalException(ClinicalErrorCode.OrderItemNotFound, "Không tìm thấy vé CLS.");
        if (ticket.RoomId != command.RoomId) throw new ClinicalException(ClinicalErrorCode.Forbidden, "Không có quyền trên vé CLS.");
        var context = await store.LockAsync(command.ServiceOrderItemId, ct)
            ?? throw new ClinicalException(ClinicalErrorCode.OrderItemNotFound, "Không tìm thấy mục CLS.");
        if (ticket.EncounterId != context.Encounter.Id) throw new ClinicalException(ClinicalErrorCode.Forbidden, "Không có quyền trên vé CLS.");
        if (context.Encounter.Status is EncounterStatus.Completed or EncounterStatus.Abandoned)
            throw new ClinicalException(ClinicalErrorCode.EncounterClosed, "Lượt khám đã kết thúc.");
        if (ticket.QueueStatus != (start ? "InProgress" : "Finished"))
            throw new ClinicalException(ClinicalErrorCode.InvalidStatusTransition, "Trạng thái vé không cho phép thao tác.");
        if (start && context.Item.ItemStatus == ItemStatus.InProgress
            || !start && context.Item.ItemStatus is ItemStatus.ServiceFinished or ItemStatus.Completed) return;
        if (context.Order.OrderBatchStatus != OrderBatchStatus.Confirmed
            || (start ? context.Item.ItemStatus is not (ItemStatus.Ordered or ItemStatus.SampleCollected) : context.Item.ItemStatus != ItemStatus.InProgress))
            throw new ClinicalException(ClinicalErrorCode.InvalidStatusTransition, "Trạng thái mục CLS không cho phép thao tác.");
        var now = await clock.GetUtcNowAsync(ct);
        context.Item.ItemStatus = start ? ItemStatus.InProgress : ItemStatus.ServiceFinished;
        if (!start) context.Item.CompletedAtUtc = now;
        await store.SaveAsync(context, !start, command, now, ct);
    }
}
