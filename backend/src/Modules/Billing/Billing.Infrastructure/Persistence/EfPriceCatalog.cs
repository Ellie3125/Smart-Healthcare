using System.Linq.Expressions;
using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Shared.Infrastructure.Persistence;
using System.Text.Json;

namespace Billing.Infrastructure.Persistence;

/// <summary>Đọc bảng giá đang hiệu lực bằng truy vấn có tham số; PriceResolver kiểm lại quy tắc chọn giá.</summary>
internal sealed class EfPriceCatalog(BillingDbContext db, SharedDbContext? shared = null) : IPriceCatalog
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
        if (db.Database.CurrentTransaction is null || shared is null) throw new InvalidOperationException("Price creation requires Billing in IUnitOfWork with audit.");
        // Rare admin writes serialize the catalog, including the empty-table case, before checking interval overlap.
        await db.Set<PriceList>().FromSqlRaw("SELECT TOP (1) * FROM billing.PriceList WITH (TABLOCKX,HOLDLOCK)").AsNoTracking().ToListAsync(ct);
        if (await db.Set<PriceList>().AnyAsync(p => p.PriceListCode == list.PriceListCode, ct))
            throw new PriceListAdminException(PriceListAdminError.CodeAlreadyExists);
        var rankIds = items.Select(i => i.DoctorRankId).ToArray();
        if (list.IsActive && await (from item in db.Set<PriceListItem>() join current in db.Set<PriceList>() on item.PriceListId equals current.Id
                where item.IsActive && item.ItemType == ItemType.ConsultationFee && item.DoctorId == null && rankIds.Contains(item.DoctorRankId)
                    && current.IsActive && current.Currency == "VND"
                    && (current.EffectiveToUtc == null || list.EffectiveFromUtc < current.EffectiveToUtc)
                    && (list.EffectiveToUtc == null || current.EffectiveFromUtc < list.EffectiveToUtc)
                select item.Id).AnyAsync(ct)) throw new PriceListAdminException(PriceListAdminError.PriceOverlap);
        db.Add(list); db.AddRange(items);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new PriceListAdminException(PriceListAdminError.CodeAlreadyExists); }
        await BillingAuditWriter.WriteAsync(db, shared, new BillingAuditEntry(actorId, "billing.PriceList", list.Id, true,
            "PriceListCode,PriceListName,EffectiveFromUtc,EffectiveToUtc,IsActive,PriceListItem", JsonSerializer.Serialize(new
            { list.PriceListCode, list.EffectiveFromUtc, list.EffectiveToUtc, list.IsActive, ItemCount = items.Count }), "Tạo bảng giá", clientIp), DateTime.UtcNow, ct);
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
