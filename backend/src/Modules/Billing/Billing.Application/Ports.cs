using Billing.Domain.Entities;

namespace Billing.Application;

/// <summary>Một dòng bảng giá kèm bảng giá chứa nó.</summary>
public sealed record PriceCandidate(PriceList PriceList, PriceListItem Item);

/// <summary>
/// Đọc bảng giá. Được phép lọc thô ở DB (đang bật, đúng loại tiền, đúng căn cứ); PriceResolver vẫn kiểm lại
/// hiệu lực theo thời điểm và tính duy nhất.
/// </summary>
public interface IPriceCatalog
{
    Task<IReadOnlyList<PriceCandidate>> GetConsultationCandidatesAsync(Guid doctorId, Guid doctorRankId, DateTime atUtc, CancellationToken ct);

    Task<IReadOnlyList<PriceCandidate>> GetClsCandidatesAsync(IReadOnlyCollection<Guid> medicalServiceIds, DateTime atUtc, CancellationToken ct);
}

/// <summary>Lưu và đọc khoản thu (billing.BillingItem).</summary>
public interface IChargeStore
{
    Task<IReadOnlyList<BillingItem>> GetByServiceOrderItemsAsync(IReadOnlyCollection<Guid> serviceOrderItemIds, CancellationToken ct);

    Task<IReadOnlyList<BillingItem>> GetByEncounterAsync(Guid encounterId, CancellationToken ct);

    void Add(BillingItem item);

    Task SaveChangesAsync(CancellationToken ct);
}
