using System.Security.Cryptography;
using System.Text.Json;
using Billing.Contracts;
using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Clinical.Application.Orders;

public sealed record ClsOrderRequest(IReadOnlyList<Guid>? ServiceIds);
public sealed record OrderedClsBatch(ServiceOrderDto Order, ClsTicketsCreated Tickets);
public enum ClsOrderError { InvalidInput, Forbidden, EncounterNotFound, EncounterClosed, ClsBatchInProgress, MedicalServiceUnavailable, IdempotencyConflict, RequestInProgress }
public sealed class ClsOrderException(ClsOrderError error) : Exception(error.ToString())
{
    public ClsOrderError Error { get; } = error;
}
public interface IClsOrderStore
{
    Task<Encounter?> LockEncounterAsync(Guid encounterId, CancellationToken ct);
    Task<int> NextRoundAsync(Guid encounterId, CancellationToken ct);
    Task AddAsync(ServiceOrder order, IReadOnlyList<ServiceOrderItem> items, CancellationToken ct);
    Task ConfirmAsync(ServiceOrder order, IReadOnlyList<ServiceOrderItem> items, Encounter encounter, Guid userId, CancellationToken ct);
}

public sealed class ClsOrderService(IClsOrderStore store, IClsServiceDirectory directory, IBillingModule billing,
    ISchedulingModule scheduling, IUnitOfWork uow, IIdempotencyStore idempotency, IDbClock clock)
{
    public const string Operation = "Clinical.OrderCls";
    public Task<OrderedClsBatch> OrderAsync(Guid encounterId, Guid doctorId, Guid userId, ClsOrderRequest request, string key, CancellationToken ct)
    {
        if (encounterId == Guid.Empty || doctorId == Guid.Empty || userId == Guid.Empty || request.ServiceIds is not { Count: > 0 and <= 50 }
            || request.ServiceIds.Contains(Guid.Empty) || request.ServiceIds.Distinct().Count() != request.ServiceIds.Count
            || string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(c => c < '!' || c > '~'))
            throw new ClsOrderException(ClsOrderError.InvalidInput);
        var ids = request.ServiceIds.Order().ToArray();
        var caller = "AppUser:" + userId.ToString("D");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { encounterId, doctorId, ids })));
        return uow.ExecuteAsync(async t =>
        {
            var encounter = await store.LockEncounterAsync(encounterId, t) ?? throw new ClsOrderException(ClsOrderError.EncounterNotFound);
            if (encounter.OriginalDoctorId != doctorId) throw new ClsOrderException(ClsOrderError.Forbidden);
            if (encounter.Status is EncounterStatus.Completed or EncounterStatus.Abandoned) throw new ClsOrderException(ClsOrderError.EncounterClosed);
            switch (await idempotency.BeginAsync(caller, Operation, key, hash, TimeSpan.FromHours(24), null, t))
            {
                case IdempotencyBeginResult.Replay replay: return JsonSerializer.Deserialize<OrderedClsBatch>(replay.BodyJson!)!;
                case IdempotencyBeginResult.Conflict: throw new ClsOrderException(ClsOrderError.IdempotencyConflict);
                case IdempotencyBeginResult.InProgress: throw new ClsOrderException(ClsOrderError.RequestInProgress);
            }
            // Sau idempotency để gửi lại cùng khóa vẫn trả kết quả cũ. Đợt trước chưa xong thì không mở đợt mới: tránh vé quay lại thứ hai (Phạm vi §5.6, làm hết đợt rồi mới quay lại bác sĩ).
            if (encounter.Status == EncounterStatus.WaitingCLS) throw new ClsOrderException(ClsOrderError.ClsBatchInProgress);
            var services = await directory.GetAsync(ids, t);
            if (services.Count != ids.Length) throw new ClsOrderException(ClsOrderError.MedicalServiceUnavailable);
            var now = await clock.GetUtcNowAsync(t);
            var order = new ServiceOrder { Id = Guid.CreateVersion7(), EncounterId = encounterId,
                OrderRoundNumber = await store.NextRoundAsync(encounterId, t), OrderedAtUtc = now, PrescribedByDoctorId = doctorId, OrderBatchStatus = OrderBatchStatus.Draft };
            var items = services.Select(s => new ServiceOrderItem { Id = Guid.CreateVersion7(), ServiceOrderId = order.Id,
                MedicalServiceId = s.Id, ServiceNameSnapshot = s.Name, IsMandatory = s.IsMandatory, ItemStatus = ItemStatus.Ordered }).ToArray();
            await store.AddAsync(order, items, t);
            var charges = await billing.CreateClsChargesAsync(new(encounterId, encounter.PatientId, order.Id, now,
                items.Select(i => new ClsChargeLine(i.Id, i.MedicalServiceId, i.ServiceNameSnapshot, 1)).ToArray()), t);
            foreach (var item in items) item.AppliedPrice = charges.Single(c => c.ServiceOrderItemId == item.Id).UnitPrice;
            var tickets = await scheduling.CreateClsTicketsAsync(new(encounterId, order.Id, items.Select(i => new ClsTicketLine(i.Id, i.MedicalServiceId)).ToArray(), null), t);
            order.OrderBatchStatus = OrderBatchStatus.Confirmed;
            encounter.Status = EncounterStatus.WaitingCLS;
            await store.ConfirmAsync(order, items, encounter, userId, t);
            var result = new OrderedClsBatch(new(order.Id, encounterId, order.OrderRoundNumber, now, doctorId,
                order.OrderBatchStatus.ToString(), order.HasActiveReturnTicket, items.Select(i => ClinicalMappers.ToDto(i, null)).ToArray()), tickets);
            await idempotency.CompleteAsync(caller, Operation, key, 201, JsonSerializer.Serialize(result), null, t);
            return result;
        }, ct);
    }
}
