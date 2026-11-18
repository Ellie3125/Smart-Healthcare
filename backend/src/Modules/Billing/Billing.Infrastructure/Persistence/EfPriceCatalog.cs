using System.Linq.Expressions;
using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Shared.Infrastructure.Persistence;
using System.Text.Json;
using Shared.Abstractions;

namespace Billing.Infrastructure.Persistence;

/// <summary>Đọc bảng giá đang hiệu lực bằng truy vấn có tham số; PriceResolver kiểm lại quy tắc chọn giá.</summary>
internal sealed class EfPriceCatalog(BillingDbContext db, SharedDbContext? shared = null, IDbClock? clock = null) : IPriceCatalog
{
    public async Task<IReadOnlyList<CatalogPriceList>> ListAsync(int skip, int take, CancellationToken ct)
    {
        var lists = await db.Set<PriceList>().AsNoTracking().OrderByDescending(p => p.EffectiveFromUtc).ThenBy(p => p.Id)
            .Skip(skip).Take(take).ToListAsync(ct);
        var ids = lists.Select(p => p.Id).ToArray();
        var items = await db.Set<PriceListItem>().AsNoTracking().Where(i => ids.Contains(i.PriceListId)).ToListAsync(ct);
        var lookup = items.ToLookup(i => i.PriceListId);
        return lists.Select(p => new CatalogPriceList(p, lookup[p.Id].ToArray())).ToArray();
    }

    public async Task CreateAsync(PriceList list, IReadOnlyList<PriceListItem> items, Guid actorId, string? clientIp, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null || shared is null || clock is null) throw new InvalidOperationException("Price creation requires Billing in IUnitOfWork with audit and DB clock.");
        // Rare admin writes serialize the catalog, including the empty-table case, before checking interval overlap.
        await db.Set<PriceList>().FromSqlRaw("SELECT TOP (1) * FROM billing.PriceList WITH (TABLOCKX,HOLDLOCK)").AsNoTracking().ToListAsync(ct);
        if (await db.Set<PriceList>().AnyAsync(p => p.PriceListCode == list.PriceListCode, ct))
            throw new PriceListAdminException(PriceListAdminError.CodeAlreadyExists);
        var rankIds = items.Select(i => i.DoctorRankId).ToArray();
        var overlapping = list.IsActive ? await (from item in db.Set<PriceListItem>() join current in db.Set<PriceList>() on item.PriceListId equals current.Id
                where item.IsActive && item.ItemType == ItemType.ConsultationFee && item.DoctorId == null && rankIds.Contains(item.DoctorRankId)
                    && current.IsActive && current.Currency == "VND"
                    && (current.EffectiveToUtc == null || list.EffectiveFromUtc < current.EffectiveToUtc)
                    && (list.EffectiveToUtc == null || current.EffectiveFromUtc < list.EffectiveToUtc)
                select current).Distinct().ToListAsync(ct) : [];
        if (overlapping.Any(p => p.EffectiveToUtc is not null || list.EffectiveFromUtc <= p.EffectiveFromUtc))
            throw new PriceListAdminException(PriceListAdminError.PriceOverlap);
        var ids = overlapping.Select(p => p.Id).ToArray();
        var previousItems = ids.Length == 0 ? [] : await db.Set<PriceListItem>().AsNoTracking()
            .Where(i => ids.Contains(i.PriceListId) && i.IsActive).ToListAsync(ct);
        var continuations = new List<(PriceList List, Guid SourceId, int Count)>();
        foreach (var previous in overlapping)
        {
            previous.EffectiveToUtc = list.EffectiveFromUtc;
            var keep = previousItems.Where(i => i.PriceListId == previous.Id
                && !(i.ItemType == ItemType.ConsultationFee && i.DoctorId == null && rankIds.Contains(i.DoctorRankId))).ToArray();
            if (keep.Length == 0) continue;
            // Closing a mixed seed list must not expire unrelated ranks, doctor overrides or CLS prices.
            var continuation = new PriceList { Id = Guid.CreateVersion7(), PriceListCode = "CONT-" + Guid.NewGuid().ToString("N"),
                PriceListName = "Tiếp nối " + previous.PriceListCode, Currency = previous.Currency, IsActive = true,
                EffectiveFromUtc = list.EffectiveFromUtc };
            db.Add(continuation);
            db.AddRange(keep.Select(i => new PriceListItem { Id = Guid.CreateVersion7(), PriceListId = continuation.Id,
                ItemType = i.ItemType, DoctorRankId = i.DoctorRankId, DoctorId = i.DoctorId, MedicalServiceId = i.MedicalServiceId,
                UnitPrice = i.UnitPrice, IsActive = true }));
            continuations.Add((continuation, previous.Id, keep.Length));
        }
        db.Add(list); db.AddRange(items);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new PriceListAdminException(PriceListAdminError.CodeAlreadyExists); }
        var now = await clock.GetUtcNowAsync(ct);
        foreach (var previous in overlapping)
            await BillingAuditWriter.WriteAsync(db, shared, new BillingAuditEntry(actorId, "billing.PriceList", previous.Id, false,
                "EffectiveToUtc", JsonSerializer.Serialize(new { previous.EffectiveToUtc }), "Đóng kỳ giá khi tạo bảng mới", clientIp,
                OldValuesJson: JsonSerializer.Serialize(new { EffectiveToUtc = (DateTime?)null })), now, ct);
        foreach (var continuation in continuations)
            await BillingAuditWriter.WriteAsync(db, shared, new BillingAuditEntry(actorId, "billing.PriceList", continuation.List.Id, true,
                "PriceListCode,EffectiveFromUtc,EffectiveToUtc,PriceListItem", JsonSerializer.Serialize(new
                { continuation.List.PriceListCode, continuation.List.EffectiveFromUtc, continuation.List.EffectiveToUtc, continuation.SourceId, ItemCount = continuation.Count }),
                "Giữ hiệu lực các giá không thay đổi", clientIp), now, ct);
        await BillingAuditWriter.WriteAsync(db, shared, new BillingAuditEntry(actorId, "billing.PriceList", list.Id, true,
            "PriceListCode,PriceListName,EffectiveFromUtc,EffectiveToUtc,IsActive,PriceListItem", JsonSerializer.Serialize(new
            { list.PriceListCode, list.EffectiveFromUtc, list.EffectiveToUtc, list.IsActive, ItemCount = items.Count }), "Tạo bảng giá", clientIp), now, ct);
    }

    public Task<IReadOnlyList<PriceCandidate>> GetConsultationCandidatesAsync(Guid doctorId, Guid doctorRankId, DateTime atUtc, CancellationToken ct) =>
        QueryAsync(item =>
            item.ItemType == ItemType.ConsultationFee
            && (item.DoctorId == doctorId || (item.DoctorId == null && item.DoctorRankId == doctorRankId)),
            atUtc, ct);

    public Task<IReadOnlyList<PriceCandidate>> GetClsCandidatesAsync(IReadOnlyCollection<Guid> medicalServiceIds, DateTime atUtc, CancellationToken ct)
    {
        var ids = medicalServiceIds.Select(id => (Guid?)id).ToList();
        return QueryAsync(item => item.ItemType == ItemType.CLSService && ids.Contains(item.MedicalServiceId), atUtc, ct);
    }

    // Lọc dòng giá trước khi join và chiếu kết quả để EF dịch toàn bộ điều kiện sang SQL.
    private async Task<IReadOnlyList<PriceCandidate>> QueryAsync(Expression<Func<PriceListItem, bool>> itemFilter, DateTime atUtc, CancellationToken ct)
    {
        var rows = await (
                from item in db.Set<PriceListItem>().AsNoTracking().Where(itemFilter)
                join list in db.Set<PriceList>().AsNoTracking() on item.PriceListId equals list.Id
                where item.IsActive
                      && list.IsActive
                      && list.Currency == BillingCurrency.Vnd
                      && list.EffectiveFromUtc <= atUtc
                      && (list.EffectiveToUtc == null || list.EffectiveToUtc > atUtc)
                select new { list, item })
            .ToListAsync(ct);

        return rows.Select(r => new PriceCandidate(r.list, r.item)).ToList();
    }
}
