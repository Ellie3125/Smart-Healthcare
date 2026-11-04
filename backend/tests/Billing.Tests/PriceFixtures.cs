using Billing.Application;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Tests;

/// <summary>Dữ liệu giá giả lập cho test; không phải mức giá thật của bệnh viện.</summary>
internal static class PriceFixtures
{
    public static readonly DateTime Jan1 = new(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc);  // 00:00 01/01/2027 giờ VN
    public static readonly DateTime Feb1 = new(2027, 1, 31, 17, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime Mid = new(2027, 1, 15, 2, 0, 0, DateTimeKind.Utc);

    public static PriceList List(string code, DateTime from, DateTime? to = null, bool active = true, string currency = "VND") =>
        new() { Id = Guid.NewGuid(), PriceListCode = code, PriceListName = code, Currency = currency, EffectiveFromUtc = from, EffectiveToUtc = to, IsActive = active };

    public static PriceCandidate RankFee(PriceList list, Guid rankId, decimal price, bool active = true) =>
        new(list, new PriceListItem { Id = Guid.NewGuid(), PriceListId = list.Id, ItemType = ItemType.ConsultationFee, DoctorRankId = rankId, UnitPrice = price, IsActive = active });

    public static PriceCandidate DoctorFee(PriceList list, Guid doctorId, decimal price) =>
        new(list, new PriceListItem { Id = Guid.NewGuid(), PriceListId = list.Id, ItemType = ItemType.ConsultationFee, DoctorId = doctorId, UnitPrice = price, IsActive = true });

    public static PriceCandidate ClsFee(PriceList list, Guid serviceId, decimal price) =>
        new(list, new PriceListItem { Id = Guid.NewGuid(), PriceListId = list.Id, ItemType = ItemType.CLSService, MedicalServiceId = serviceId, UnitPrice = price, IsActive = true });
}
