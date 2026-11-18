using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using IdentityPatients.Contracts;
using Shared.Abstractions;

namespace Billing.Application;

public sealed record CatalogPriceList(PriceList List, IReadOnlyList<PriceListItem> Items);
public sealed record ConsultationPrice(string? RankName, decimal Price);
public sealed record CreatePriceListRequest(string? Code, string? Name, string? EffectiveFrom, string? EffectiveTo,
    bool IsActive, IReadOnlyList<ConsultationPrice>? ConsultationPrices);
public sealed record AdminPriceList(Guid Id, string Code, string Name, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    bool IsActive, IReadOnlyList<ConsultationPrice> ConsultationPrices);
public sealed record AdminPriceListPage(IReadOnlyList<AdminPriceList> Items, int Page, int PageSize, bool HasMore);
public enum PriceListAdminError { InvalidInput, CodeAlreadyExists, PriceOverlap, IdempotencyConflict, RequestInProgress }
public sealed class PriceListAdminException(PriceListAdminError error) : Exception(error.ToString())
{
    public PriceListAdminError Error => error;
}

public sealed class PriceListAdminService(IPriceCatalog catalog, IIdentityPatientsModule identity,
    IUnitOfWork uow, IIdempotencyStore idempotency)
{
    public async Task<AdminPriceListPage> ListAsync(int page, int size, CancellationToken ct)
    {
        if (page is < 1 or > 1000 || size is < 1 or > 100) throw Invalid();
        var rows = await catalog.ListAsync((page - 1) * size, size + 1, ct);
        var ranks = (await identity.GetDoctorRanksAsync(ct)).ToDictionary(x => x.Id, x => x.Name);
        return new(rows.Take(size).Select(row => View(row, ranks)).ToArray(), page, size, rows.Count > size);
    }

    public async Task<AdminPriceList> CreateAsync(CreatePriceListRequest request, Guid actorId, string? key, string? ip, CancellationToken ct)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 64
            || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200
            || !DateOnly.TryParseExact(request.EffectiveFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            || from == default || request.ConsultationPrices is not { Count: > 0 and <= 50 }
            || key is not null && (key.Length is < 1 or > 128 || key.Any(c => c < '!' || c > '~'))) throw Invalid();
        DateOnly? to = null;
        if (request.EffectiveTo is not null)
        {
            if (!DateOnly.TryParseExact(request.EffectiveTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || end <= from)
                throw Invalid();
            to = end;
        }
        foreach (var price in request.ConsultationPrices)
        {
            if (price is null || string.IsNullOrWhiteSpace(price.RankName) || price.Price < 0 || price.Price > 9999999999999999.99m
                || decimal.Round(price.Price, 2) != price.Price) throw Invalid();
        }
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Code = request.Code.Trim(), Name = request.Name.Trim(),
            From = from, To = to, request.IsActive, Prices = request.ConsultationPrices
                .Select(p => new { Rank = p.RankName!.Trim().ToUpperInvariant(), Price = p.Price.ToString("0.00", CultureInfo.InvariantCulture) }).OrderBy(p => p.Rank) })));
        const string operation = "Billing.CreatePriceList";
        var caller = "AppUser:" + actorId.ToString("D");
        return await uow.ExecuteAsync(async token =>
        {
            if (key is not null)
            {
                switch (await idempotency.BeginAsync(caller, operation, key, hash, TimeSpan.FromHours(24), null, token))
                {
                    case IdempotencyBeginResult.Replay replay: return JsonSerializer.Deserialize<AdminPriceList>(replay.BodyJson!)!;
                    case IdempotencyBeginResult.Conflict: throw new PriceListAdminException(PriceListAdminError.IdempotencyConflict);
                    case IdempotencyBeginResult.InProgress: throw new PriceListAdminException(PriceListAdminError.RequestInProgress);
                }
            }
            var ranks = await identity.GetDoctorRanksAsync(token);
            var list = new PriceList { Id = Guid.CreateVersion7(), PriceListCode = request.Code.Trim(), PriceListName = request.Name.Trim(),
                Currency = "VND", EffectiveFromUtc = Midnight(from), EffectiveToUtc = to.HasValue ? Midnight(to.Value) : null, IsActive = request.IsActive };
            var items = new List<PriceListItem>();
            foreach (var price in request.ConsultationPrices)
            {
                var matches = ranks.Where(r => string.Equals(r.Name, price.RankName!.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length != 1 || items.Any(i => i.DoctorRankId == matches[0].Id)) throw Invalid();
                items.Add(new PriceListItem { Id = Guid.CreateVersion7(), PriceListId = list.Id, ItemType = ItemType.ConsultationFee,
                    DoctorRankId = matches[0].Id, UnitPrice = price.Price, IsActive = true });
            }
            await catalog.CreateAsync(list, items, actorId, ip, token);
            var result = View(new(list, items), ranks.ToDictionary(r => r.Id, r => r.Name));
            if (key is not null) await idempotency.CompleteAsync(caller, operation, key, 201, JsonSerializer.Serialize(result), null, token);
            return result;
        }, ct);
    }

    private static AdminPriceList View(CatalogPriceList row, IReadOnlyDictionary<Guid, string> ranks) => new(row.List.Id,
        row.List.PriceListCode, row.List.PriceListName, DateOnly.FromDateTime(row.List.EffectiveFromUtc.AddHours(7)),
        row.List.EffectiveToUtc.HasValue ? DateOnly.FromDateTime(row.List.EffectiveToUtc.Value.AddHours(7)) : null, row.List.IsActive,
        row.Items.Where(i => i.ItemType == ItemType.ConsultationFee && i.DoctorId is null && i.DoctorRankId.HasValue && i.IsActive)
            .OrderBy(i => i.DoctorRankId).Select(i => new ConsultationPrice(ranks.GetValueOrDefault(i.DoctorRankId!.Value, i.DoctorRankId.Value.ToString()), i.UnitPrice)).ToArray());
    private static DateTime Midnight(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
    private static PriceListAdminException Invalid() => new(PriceListAdminError.InvalidInput);
}
