using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Queues;

public sealed class SqlClsTicketReader(SchedulingDbContext db) : IClsTicketReader
{
    public Task<ClsTicketContext?> GetByIdAsync(Guid ticketId, CancellationToken ct) =>
        Context(db.Set<QueueTicket>().Where(x => x.Id == ticketId)).SingleOrDefaultAsync(ct);
    public Task<ClsTicketContext?> GetByItemAsync(Guid orderItemId, CancellationToken ct) =>
        Context(db.Set<QueueTicket>().Where(x => x.ServiceOrderItemId == orderItemId)).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<ClsTicketContext>> GetByEncounterRoomAsync(Guid encounterId, Guid roomId, CancellationToken ct) =>
        await Context(db.Set<QueueTicket>().Where(x => x.EncounterId == encounterId && x.TargetRoomId == roomId)).ToListAsync(ct);
    private static IQueryable<ClsTicketContext> Context(IQueryable<QueueTicket> query) => query.AsNoTracking()
        .Where(x => x.TicketType == TicketType.CLS && x.ServiceOrderItemId != null)
        .Select(x => new ClsTicketContext(x.Id, x.EncounterId, x.ServiceOrderItemId!.Value, x.TargetRoomId, x.QueueStatus.ToString(), x.TicketNumber));
}
