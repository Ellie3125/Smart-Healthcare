using Billing.Contracts;
using Billing.Domain.Enums;

namespace Billing.Application;

/// <summary>
/// Chọn đúng một đơn giá đang hiệu lực. Quy tắc (contract v0.1 §4):
/// chỉ bảng giá IsActive, VND, EffectiveFromUtc ≤ t &lt; EffectiveToUtc; dòng IsActive.
/// Phí khám: giá riêng của bác sĩ trước, không có mới dùng giá theo cấp bậc (dòng không gắn bác sĩ).
/// Không có dòng nào → PriceNotConfigured; nhiều dòng cùng căn cứ → AmbiguousPrice (không tự chọn).
/// </summary>
public static class PriceResolver
{
    public static FeeQuote ResolveExamFee(IEnumerable<PriceCandidate> candidates, Guid doctorId, Guid doctorRankId, DateTime atUtc)
    {
        var effective = Effective(candidates, atUtc, ItemType.ConsultationFee).ToList();

        var doctorSpecific = effective.Where(c => c.Item.DoctorId == doctorId).ToList();
        if (doctorSpecific.Count > 0)
            return ToQuote(Single(doctorSpecific, $"bác sĩ {doctorId}"), ChargeType.ConsultationFee, PriceBasis.Doctor);

        var rankBased = effective.Where(c => c.Item.DoctorId is null && c.Item.DoctorRankId == doctorRankId).ToList();
        if (rankBased.Count > 0)
            return ToQuote(Single(rankBased, $"cấp bậc {doctorRankId}"), ChargeType.ConsultationFee, PriceBasis.DoctorRank);

        throw new BillingException(BillingErrorCode.PriceNotConfigured,
            $"Chưa có phí khám đang hiệu lực cho bác sĩ {doctorId} hoặc cấp bậc {doctorRankId}.");
    }

    public static FeeQuote ResolveClsFee(IEnumerable<PriceCandidate> candidates, Guid medicalServiceId, DateTime atUtc)
    {
        var matches = Effective(candidates, atUtc, ItemType.CLSService)
            .Where(c => c.Item.MedicalServiceId == medicalServiceId)
            .ToList();
        if (matches.Count == 0)
            throw new BillingException(BillingErrorCode.PriceNotConfigured,
                $"Chưa có giá đang hiệu lực cho dịch vụ CLS {medicalServiceId}.");

        return ToQuote(Single(matches, $"dịch vụ CLS {medicalServiceId}"), ChargeType.CLSService, PriceBasis.MedicalService);
    }

    private static IEnumerable<PriceCandidate> Effective(IEnumerable<PriceCandidate> candidates, DateTime atUtc, ItemType itemType)
    {
        Guard.Utc(atUtc, nameof(atUtc));
        return candidates.Where(c =>
            c.Item.IsActive
            && c.Item.ItemType == itemType
            && c.Item.PriceListId == c.PriceList.Id
            && string.Equals(c.PriceList.Currency, BillingCurrency.Vnd, StringComparison.Ordinal)
            && c.PriceList.IsEffectiveAt(atUtc));
    }

    private static PriceCandidate Single(List<PriceCandidate> matches, string basis) =>
        matches.Count == 1
            ? matches[0]
            : throw new BillingException(BillingErrorCode.AmbiguousPrice,
                $"Có {matches.Count} đơn giá đang hiệu lực cho {basis} (bảng giá: {string.Join(", ", matches.Select(m => m.PriceList.PriceListCode).Distinct())}); cần sửa cấu hình bảng giá.");

    private static FeeQuote ToQuote(PriceCandidate c, ChargeType chargeType, PriceBasis basis) =>
        new(c.Item.Id, c.PriceList.PriceListCode, chargeType, basis, c.Item.MedicalServiceId, c.Item.UnitPrice, c.PriceList.Currency);
}

internal static class Guard
{
    public static void Utc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new BillingException(BillingErrorCode.InvalidRequest, $"{name} phải là UTC (Kind = Utc).");
    }
}
