using IdentityPatients.Application.Administration;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure;

namespace IdentityPatients.Infrastructure.Services;

internal sealed class AdminAuditLogReader(AuditLogAdminReader reader, IdentityPatientsDbContext db) : IAdminAuditLogReader
{
    public async Task<AdminAuditLogPage> ReadAsync(string? keyword, int page, CancellationToken ct)
    {
        var result = await reader.ReadAsync(keyword, page, ct);
        var ids = result.Items.Where(x => x.ActorId.HasValue).Select(x => x.ActorId!.Value).Distinct().ToArray();
        var actors = await db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new
        {
            u.Id, u.UserName,
            Staff = db.Staffs.Where(s => s.AppUserId == u.Id).Select(s => new { s.FullName, s.StaffType }).FirstOrDefault(),
            HasRoom = db.RoomAccounts.Any(r => r.AppUserId == u.Id)
        }).ToDictionaryAsync(x => x.Id, ct);
        return new(result.Items.Select(row =>
        {
            var actor = row.ActorId is { } id ? actors.GetValueOrDefault(id) : null;
            return new AdminAuditLogEntry(row.Id, row.OccurredAt,
                actor?.Staff?.FullName ?? actor?.UserName ?? row.ActorId?.ToString() ?? "System",
                actor is null ? "" : AccountRoles.RoleFor(actor.HasRoom, actor.Staff?.StaffType) ?? "",
                row.Action, row.EntityType, row.EntityId, row.BeforeJson, row.AfterJson);
        }).ToArray(), result.HasMore);
    }
}
