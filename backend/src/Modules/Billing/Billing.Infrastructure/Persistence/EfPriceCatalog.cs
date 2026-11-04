using System.Linq.Expressions;
using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence;

/// <summary>Đọc bảng giá đang hiệu lực bằng truy vấn có tham số; PriceResolver kiểm lại quy tắc chọn giá.</summary>
internal sealed class EfPriceCatalog(BillingDbContext db) : IPriceCatalog
{
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
