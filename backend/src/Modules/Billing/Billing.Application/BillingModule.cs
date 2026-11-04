using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Application;

/// <summary>Hiện thực IBillingModule (contract v0.1 Draft). Không gọi cổng thanh toán (MOD-05).</summary>
public sealed class BillingModule(IPriceCatalog catalog, IChargeStore charges, TimeProvider clock) : IBillingModule
{
    public async Task<FeeQuote> QuoteExamFeeAsync(QuoteExamFee query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        Guard.Utc(query.AtUtc, nameof(query.AtUtc));
        var candidates = await catalog.GetConsultationCandidatesAsync(query.DoctorId, query.DoctorRankId, query.AtUtc, ct);
        return PriceResolver.ResolveExamFee(candidates, query.DoctorId, query.DoctorRankId, query.AtUtc);
    }

    public async Task<IReadOnlyList<FeeQuote>> QuoteClsFeesAsync(QuoteClsFees query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        Guard.Utc(query.AtUtc, nameof(query.AtUtc));
        if (query.MedicalServiceIds is not { Count: > 0 })
            throw Invalid("Cần ít nhất một dịch vụ CLS.");
        if (query.MedicalServiceIds.Distinct().Count() != query.MedicalServiceIds.Count)
            throw Invalid("Danh sách dịch vụ CLS bị trùng.");

        var candidates = await catalog.GetClsCandidatesAsync(query.MedicalServiceIds, query.AtUtc, ct);
        return query.MedicalServiceIds.Select(id => PriceResolver.ResolveClsFee(candidates, id, query.AtUtc)).ToList();
    }

    public async Task<ChargeDto> CreateExamChargeAsync(CreateExamCharge command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateName(command.ItemNameSnapshot);

        var quote = await QuoteExamFeeAsync(new QuoteExamFee(command.DoctorId, command.DoctorRankId, command.PricedAtUtc), ct);
        var item = BillingItem.CreatePending(
            quote.PriceListItemId, ItemType.ConsultationFee, command.ItemNameSnapshot, quote.UnitPrice, 1, clock.GetUtcNow().UtcDateTime);

        charges.Add(item);
        await charges.SaveChangesAsync(ct);
        return ChargeMapper.ToDto(item);
    }

    public async Task<IReadOnlyList<ChargeDto>> CreateClsChargesAsync(CreateClsCharges command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.EncounterId == Guid.Empty)
            throw Invalid("Thiếu EncounterId.");
        if (command.Lines is not { Count: > 0 })
            throw Invalid("Cần ít nhất một dòng chỉ định CLS.");
        if (command.Lines.Select(l => l.ServiceOrderItemId).Distinct().Count() != command.Lines.Count)
            throw Invalid("ServiceOrderItemId bị trùng trong cùng yêu cầu.");
        foreach (var line in command.Lines)
        {
            ValidateName(line.ServiceNameSnapshot);
            if (line.Quantity <= 0)
                throw Invalid($"Số lượng của dòng {line.ServiceOrderItemId} phải lớn hơn 0.");
        }

        var activeCharges = (await charges.GetByServiceOrderItemsAsync(command.Lines.Select(l => l.ServiceOrderItemId).ToList(), ct))
            .Where(c => c.Status != BillingItemStatus.Cancelled)
            .GroupBy(c => c.ServiceOrderItemId!.Value)
            .ToList();

        // ERD v2.4 không có unique trên BillingItem.ServiceOrderItemId; dữ liệu trùng phải được người xử lý, không tự chọn một khoản.
        var duplicated = activeCharges.Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicated.Count > 0)
            throw new BillingException(BillingErrorCode.DuplicateCharge,
                $"ServiceOrderItem {string.Join(", ", duplicated)} đang có nhiều hơn một khoản thu chưa hủy; cần đối soát trước khi tạo hoặc trả khoản thu.");

        var existing = activeCharges.ToDictionary(g => g.Key, g => g.Single());

        foreach (var line in command.Lines.Where(l => existing.TryGetValue(l.ServiceOrderItemId, out _)))
        {
            if (existing[line.ServiceOrderItemId].EncounterId != command.EncounterId)
                throw Invalid($"ServiceOrderItem {line.ServiceOrderItemId} đã có khoản thu thuộc lượt khám khác.");
        }

        var newLines = command.Lines.Where(l => !existing.ContainsKey(l.ServiceOrderItemId)).ToList();
        var quotes = newLines.Count == 0
            ? new Dictionary<Guid, FeeQuote>()
            : (await QuoteClsFeesAsync(new QuoteClsFees(newLines.Select(l => l.MedicalServiceId).Distinct().ToList(), command.PricedAtUtc), ct))
                .ToDictionary(q => q.MedicalServiceId!.Value);

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var line in newLines)
        {
            var quote = quotes[line.MedicalServiceId];
            var item = BillingItem.CreatePending(
                quote.PriceListItemId, ItemType.CLSService, line.ServiceNameSnapshot, quote.UnitPrice, line.Quantity, now,
                encounterId: command.EncounterId, serviceOrderItemId: line.ServiceOrderItemId);
            charges.Add(item);
            existing[line.ServiceOrderItemId] = item;
        }

        if (newLines.Count > 0)
            await charges.SaveChangesAsync(ct);

        return command.Lines.Select(l => ChargeMapper.ToDto(existing[l.ServiceOrderItemId])).ToList();
    }

    public async Task<EncounterCostSummary> GetEncounterCostSummaryAsync(Guid encounterId, CancellationToken ct)
    {
        if (encounterId == Guid.Empty)
            throw Invalid("Thiếu EncounterId.");
        return CostSummaryCalculator.Summarize(encounterId, await charges.GetByEncounterAsync(encounterId, ct));
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > BillingItem.ItemNameMaxLength)
            throw Invalid($"Tên khoản thu bắt buộc, tối đa {BillingItem.ItemNameMaxLength} ký tự.");
    }

    private static BillingException Invalid(string message) => new(BillingErrorCode.InvalidRequest, message);
}
