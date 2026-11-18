using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Billing.Contracts;
using Billing.Domain.Entities;
using IdentityPatients.Contracts;
using Shared.Abstractions;
using System.Text.Json.Serialization;

namespace Billing.Application;

public sealed record CloseCashierShift([property: JsonRequired] decimal ActualCashCounted, string? DiscrepancyNote);
public enum CashierShiftError { InvalidInput, IdempotencyConflict, RequestInProgress }
public sealed class CashierShiftException(CashierShiftError error) : Exception(error.ToString())
{
    public CashierShiftError Error => error;
}

public sealed class CashierShiftService(ICashierStore shifts, CashierService cashier, IUnitOfWork uow,
    IIdempotencyStore idempotency, IIdentityPatientsModule identity, IDbClock clock)
{
    public async Task<CashierShiftSummary> ReadAsync(Guid actor, CancellationToken ct) =>
        (await cashier.GetTodaySummaryAsync(actor, ct)) with { CashierName = (await identity.GetActiveStaffAsync(actor, ct))?.FullName ?? "" };

    public Task<CashierShiftSummary> CloseAsync(Guid actor, CloseCashierShift request, string key, string? ip, CancellationToken ct)
    {
        var note = request.DiscrepancyNote?.Trim();
        if (actor == Guid.Empty || request.ActualCashCounted < 0 || request.ActualCashCounted > 9999999999999999.99m
            || decimal.Round(request.ActualCashCounted, 2) != request.ActualCashCounted || note?.Length > 500
            || string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(c => c < '!' || c > '~'))
            throw new CashierShiftException(CashierShiftError.InvalidInput);
        note = string.IsNullOrEmpty(note) ? null : note;
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { Amount = request.ActualCashCounted.ToString("0.00", CultureInfo.InvariantCulture), Note = note })));
        var caller = "AppUser:" + actor.ToString("D"); const string operation = "Billing.CloseCashierShift";
        return uow.ExecuteAsync(async token =>
        {
            switch (await idempotency.BeginAsync(caller, operation, key, hash, TimeSpan.FromHours(24), null, token))
            {
                case IdempotencyBeginResult.Replay replay: return JsonSerializer.Deserialize<CashierShiftSummary>(replay.BodyJson!)!;
                case IdempotencyBeginResult.Conflict: throw new CashierShiftException(CashierShiftError.IdempotencyConflict);
                case IdempotencyBeginResult.InProgress: throw new CashierShiftException(CashierShiftError.RequestInProgress);
            }
            var date = DateOnly.FromDateTime((await clock.GetUtcNowAsync(token)).AddHours(7));
            if (await shifts.LockClosureAsync(actor, date, token) is not null)
                throw new BillingException(BillingErrorCode.CashierShiftClosed, "Ngày thu ngân đã được chốt.");
            var summary = await cashier.GetTodaySummaryAsync(actor, token);
            var now = await clock.GetUtcNowAsync(token);
            if (summary.ShiftDate != date || DateOnly.FromDateTime(now.AddHours(7)) != date)
                throw new BillingException(BillingErrorCode.CashierShiftDateChanged, "Ngày thu ngân đã đổi; vui lòng gửi lại.");
            if (request.ActualCashCounted != summary.NetCash && string.IsNullOrWhiteSpace(note))
                throw new CashierShiftException(CashierShiftError.InvalidInput);
            var closure = new CashierShiftClosure { Id = Guid.CreateVersion7(), CashierUserId = actor, ShiftDate = summary.ShiftDate,
                ReceiptCount = summary.ReceiptCount, RefundCount = summary.Refunds.Count, ExpectedCashCollected = summary.CashCollected,
                ExpectedCashRefunded = summary.CashRefunded, ActualCashCounted = request.ActualCashCounted, DiscrepancyNote = note,
                Currency = summary.Currency, ClosedAtUtc = now };
            await shifts.SaveClosureAsync(closure, ip, token);
            var result = summary with { IsClosed = true, ClosedAt = now, ActualCashCounted = request.ActualCashCounted, DiscrepancyNote = note,
                CashierName = (await identity.GetActiveStaffAsync(actor, token))?.FullName ?? "" };
            await idempotency.CompleteAsync(caller, operation, key, 200, JsonSerializer.Serialize(result), null, token);
            return result;
        }, ct);
    }
}
