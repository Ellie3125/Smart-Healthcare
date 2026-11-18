using Clinical.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Queues;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Queues;

internal sealed class SqlClsPendingResultReader(SchedulingDbContext db, IClinicalModule clinical) : IClsPendingResultReader
{
    public async Task<ClsPendingResultPage> ListAsync(Guid roomId, int page, int pageSize, CancellationToken ct)
    {
        if (roomId == Guid.Empty || page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentException("Phòng hoặc phân trang không hợp lệ.");
        var tickets = db.Set<QueueTicket>().AsNoTracking().Where(t => t.TargetRoomId == roomId && t.TicketType == TicketType.CLS
            && t.QueueStatus == QueueStatus.Finished && t.ServiceOrderItemId != null)
            .OrderBy(t => t.TicketNumber).ThenBy(t => t.Id)
            .Select(t => new ClsPendingResult(t.Id, t.TicketNumber, t.ServiceOrderItemId!.Value, t.EncounterId, t.CompletedAtUtc));
        var orders = new Dictionary<Guid, IReadOnlyList<ServiceOrderDto>>(); var items = new List<ClsPendingResult>();
        var skip = (page - 1) * pageSize;
        // shortcut: Clinical exposes orders per encounter only; use a batch pending-results contract when room history grows.
        foreach (var ticket in await tickets.ToListAsync(ct))
        {
            if (!orders.TryGetValue(ticket.EncounterId, out var encounterOrders))
                orders[ticket.EncounterId] = encounterOrders = await clinical.GetServiceOrdersByEncounterIdAsync(ticket.EncounterId, ct);
            var item = encounterOrders.Where(o => o.OrderBatchStatus != "Cancelled").SelectMany(o => o.Items)
                .SingleOrDefault(i => i.Id == ticket.ServiceOrderItemId);
            if (item is not { ItemStatus: "ServiceFinished" or "Completed" } || item.Result?.ResultStatus == "Published") continue;
            if (skip > 0) { skip--; continue; }
            items.Add(ticket);
            if (items.Count > pageSize) break;
        }
        return new(items.Take(pageSize).ToArray(), items.Count > pageSize);
    }
}
