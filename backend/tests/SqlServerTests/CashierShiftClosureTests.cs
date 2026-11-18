using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Billing.Api;
using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure;
using Billing.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(BillingPaymentSerialCollection))]
public sealed class CashierShiftClosureTests
{
    private const string Route = "/api/billing/staff/cashier/close-shift";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Close_requires_receptionist_staff_and_a_valid_key()
    {
        await using var f = await ShiftHost.CreateAsync();
        using var anonymous = f.Client(authenticate: false);
        using var denied = await f.CloseAsync(anonymous, 0, "", "anon");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Receptionist, Channels.Patient), (Roles.Patient, Channels.Patient),
                     (Roles.Admin, Channels.Staff), (Roles.Doctor, Channels.Staff), (Roles.Nurse, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
        {
            using var wrong = f.Client(role: role, channel: channel);
            using var response = await f.CloseAsync(wrong, 0, "", Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var client = f.Client();
        foreach (var key in new[] { null, "", "bad key", new string('x', 129) })
        { using var response = await f.CloseAsync(client, 0, "", key); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        foreach (var text in new[] { "{", "null", "{}" })
        {
            using var malformed = new HttpRequestMessage(HttpMethod.Post, Route) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
            malformed.Headers.Add("Idempotency-Key", "body");
            using var response = await client.SendAsync(malformed, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var plain = await client.PostAsync(Route, new StringContent("plain"), Ct);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, plain.StatusCode);
    }

    [Fact]
    public async Task Matching_close_snapshots_summary_audit_and_replays_but_new_key_is_409()
    {
        await using var f = await ShiftHost.CreateAsync(); using var client = f.Client();
        var batch = await f.BatchAsync(100m);
        using var paid = await f.CollectAsync(client, batch.Order); Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        using var closed = await f.CloseAsync(client, 100m, "", "close");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var json = await closed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(json.GetProperty("isClosed").GetBoolean());
        Assert.Equal(100m, json.GetProperty("expectedCashCollected").GetDecimal());
        Assert.Equal(0m, json.GetProperty("expectedCashRefunded").GetDecimal());
        Assert.Equal(100m, json.GetProperty("netExpectedCash").GetDecimal());
        Assert.NotEqual(JsonValueKind.Null, json.GetProperty("closedAt").ValueKind);
        using var replay = await f.CloseAsync(client, 100m, "", "close");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(await closed.Content.ReadAsStringAsync(Ct), await replay.Content.ReadAsStringAsync(Ct));
        using var conflict = await f.CloseAsync(client, 99m, "different", "close");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var repeat = await f.CloseAsync(client, 100m, "", "another");
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        using var summary = await client.GetAsync("/api/billing/staff/cashier/shift-summary", Ct);
        var snapshot = await summary.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(snapshot.GetProperty("isClosed").GetBoolean());
        Assert.Equal(100m, snapshot.GetProperty("cashCollected").GetDecimal());
        await using var scope = f.App.Services.CreateAsyncScope();
        var row = Assert.Single(await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Set<CashierShiftClosure>()
            .Where(x => x.CashierUserId == f.Actor).ToListAsync(Ct));
        Assert.Equal((1, 100m, 100m), (row.ReceiptCount, row.ExpectedCashCollected, row.ActualCashCounted));
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().CountAsync(x => x.RecordId == row.Id, Ct));
    }

    [Theory]
    [InlineData(-1, "reason", false)]
    [InlineData(1, "", false)]
    [InlineData(1, "   ", false)]
    [InlineData(1, "Count differs", true)]
    public async Task Count_and_discrepancy_validation(decimal actual, string note, bool allowed)
    {
        await using var f = await ShiftHost.CreateAsync(); using var client = f.Client();
        using var response = await f.CloseAsync(client, actual, note, "count");
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Overlong_note_and_excess_precision_are_400()
    {
        await using var f = await ShiftHost.CreateAsync(); using var client = f.Client();
        using var longNote = await f.CloseAsync(client, 0, new string('x', 501), "note");
        Assert.Equal(HttpStatusCode.BadRequest, longNote.StatusCode);
        using var precision = await f.CloseAsync(client, 0.001m, "reason", "precision");
        Assert.Equal(HttpStatusCode.BadRequest, precision.StatusCode);
    }

    [Fact]
    public async Task Two_parallel_closes_have_one_winner_and_a_collection_race_keeps_a_consistent_snapshot()
    {
        await using var f = await ShiftHost.CreateAsync(); using var first = f.Client(); using var second = f.Client();
        var responses = await Task.WhenAll(f.CloseAsync(first, 0, "", "one"), f.CloseAsync(second, 0, "", "two"));
        try { Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(x => x.StatusCode).OrderBy(x => (int)x)); }
        finally { foreach (var response in responses) response.Dispose(); }
        var other = f.Other;
        using var otherClient = f.Client(other);
        var batch = await f.BatchAsync(200m);
        var race = await Task.WhenAll(f.CloseAsync(otherClient, 0, "Reconcile counted cash", "race"), f.CollectAsync(otherClient, batch.Order));
        try
        {
            Assert.Equal(HttpStatusCode.OK, race[0].StatusCode);
            Assert.Contains(race[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            var snapshot = await race[0].Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(race[1].IsSuccessStatusCode ? 200m : 0m, snapshot.GetProperty("expectedCashCollected").GetDecimal());
        }
        finally { foreach (var response in race) response.Dispose(); }
    }

    [Fact]
    public async Task Closed_cashier_cannot_collect_cls_exam_or_refund_but_other_cashier_can()
    {
        await using var f = await ShiftHost.CreateAsync(); using var client = f.Client();
        var paid = await f.BatchAsync(100m); using var receipt = await f.CollectAsync(client, paid.Order);
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        var refundId = await f.RefundAsync(paid.Charge);
        using var closed = await f.CloseAsync(client, 100m, "", "close"); Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var pending = await f.BatchAsync(200m);
        using var collect = await f.CollectAsync(client, pending.Order);
        Assert.Equal(HttpStatusCode.Conflict, collect.StatusCode);
        Assert.Contains("CashierShiftClosed", await collect.Content.ReadAsStringAsync(Ct));
        using var refund = await client.PostAsJsonAsync($"/api/billing/staff/cash-refunds/{refundId}/disburse", new { }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, refund.StatusCode);
        Assert.Contains("CashierShiftClosed", await refund.Content.ReadAsStringAsync(Ct));
        await using var scope = f.App.Services.CreateAsyncScope();
        var error = await Assert.ThrowsAsync<BillingException>(() => scope.ServiceProvider.GetRequiredService<CashierService>()
            .CollectExamFeeCashAsync(new(Guid.NewGuid(), f.Patient, pending.Charge, f.Actor, null), Ct));
        Assert.Equal("CashierShiftClosed", error.Code.ToString());
        using var other = f.Client(f.Other);
        using var otherCollect = await f.CollectAsync(other, pending.Order);
        Assert.Equal(HttpStatusCode.OK, otherCollect.StatusCode);
        using var otherRefund = await other.PostAsJsonAsync($"/api/billing/staff/cash-refunds/{refundId}/disburse", new { }, Ct);
        Assert.Equal(HttpStatusCode.OK, otherRefund.StatusCode);
        using var otherClose = await f.CloseAsync(other, 180m, "", "other-close");
        Assert.Equal(HttpStatusCode.OK, otherClose.StatusCode);
        Assert.Equal(20m, (await otherClose.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("expectedCashRefunded").GetDecimal());
    }

    [Fact]
    public async Task Frozen_DB_day_keeps_receipt_refund_audit_and_snapshot_on_the_same_day()
    {
        await using var f = await ShiftHost.CreateAsync(services => services.AddSingleton<IDbClock>(new JumpClock(int.MaxValue)));
        using var client = f.Client(); var batch = await f.BatchAsync(100m);
        using var paid = await f.CollectAsync(client, batch.Order); Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var refundId = await f.RefundAsync(batch.Charge);
        using var refund = await client.PostAsJsonAsync($"/api/billing/staff/cash-refunds/{refundId}/disburse", new { }, Ct);
        Assert.Equal(HttpStatusCode.OK, refund.StatusCode);
        using var closed = await f.CloseAsync(client, 80m, "", "freeze");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var result = await closed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("2086-01-01", result.GetProperty("shiftDate").GetString());
        Assert.Equal(20m, result.GetProperty("expectedCashRefunded").GetDecimal());
        Assert.Equal(80m, result.GetProperty("netExpectedCash").GetDecimal());
    }

    [Theory]
    [InlineData("close", 3)]
    [InlineData("collect", 2)]
    [InlineData("refund", 2)]
    public async Task Day_change_while_waiting_for_the_shift_lock_is_409(string action, int threshold)
    {
        await using var f = await ShiftHost.CreateAsync(services => services.AddSingleton<IDbClock>(new JumpClock(threshold)));
        using var client = f.Client();
        using var response = action switch
        {
            "close" => await f.CloseAsync(client, 0, "", "midnight"),
            "collect" => await f.CollectAsync(client, Guid.NewGuid()),
            _ => await client.PostAsJsonAsync($"/api/billing/staff/cash-refunds/{Guid.NewGuid()}/disburse", new { }, Ct)
        };
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("CashierShiftDateChanged", await response.Content.ReadAsStringAsync(Ct));
    }

    private sealed class JumpClock(int threshold) : IDbClock
    {
        private int _calls;
        public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(new DateTime(2086, 1,
            Interlocked.Increment(ref _calls) >= threshold ? 2 : 1, 2, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task A_DB_unique_violation_is_mapped_to_shift_closed()
    {
        await using var f = await TestDatabase.OpenAsync();
        await using var db = f.CreateContext<BillingDbContext>(o => new(o));
        await using var shared = f.CreateContext<SharedDbContext>(o => new(o));
        var actor = Guid.NewGuid();
        var store = new EfCashierStore(db, shared, TimeProvider.System);
        CashierShiftClosure Row() => new() { Id = Guid.NewGuid(), CashierUserId = actor, ShiftDate = new(2086, 1, 1), ClosedAtUtc = DateTime.UtcNow };
        await store.SaveClosureAsync(Row(), null, Ct);
        var error = await Assert.ThrowsAsync<BillingException>(() => store.SaveClosureAsync(Row(), null, Ct));
        Assert.Equal(BillingErrorCode.CashierShiftClosed, error.Code);
    }

    [Fact]
    public async Task An_in_progress_key_is_409()
    {
        await using var f = await ShiftHost.CreateAsync(services => services.AddScoped<IIdempotencyStore, BusyKey>());
        using var client = f.Client(); using var response = await f.CloseAsync(client, 0, "", "busy");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("RequestInProgress", await response.Content.ReadAsStringAsync(Ct));
    }
    private sealed class BusyKey : IIdempotencyStore
    {
        public Task<IdempotencyBeginResult> BeginAsync(string callerScope, string operationType, string key, string requestHash, TimeSpan ttl,
            Guid? operationId, CancellationToken ct) => Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.InProgress(null));
        public Task CompleteAsync(string callerScope, string operationType, string key, int statusCode, string? bodyJson, Guid? operationId, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task Closure_store_refuses_to_write_without_a_transaction()
    {
        await using var f = await ShiftHost.CreateAsync();
        await using var scope = f.App.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ICashierStore>()
            .SaveClosureAsync(new CashierShiftClosure { Id = Guid.NewGuid(), CashierUserId = f.Actor, ShiftDate = new(2086, 1, 1), ClosedAtUtc = DateTime.UtcNow }, null, Ct));
        Assert.False(await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Set<CashierShiftClosure>().AnyAsync(x => x.CashierUserId == f.Actor, Ct));
    }

    [Fact]
    public async Task Audit_failure_rolls_back_closure_and_idempotency()
    {
        await using var f = await ShiftHost.CreateAsync(services => services.AddDbContext<SharedDbContext>(o =>
            o.UseSqlServer(TestDatabase.RequiredConnectionString()).AddInterceptors(new FailAudit())));
        using var client = f.Client(); using var response = await f.CloseAsync(client, 0, "", "audit");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var scope = f.App.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Set<CashierShiftClosure>().AnyAsync(x => x.CashierUserId == f.Actor, Ct));
        Assert.False(await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<ApiIdempotencyRecord>()
            .AnyAsync(x => x.CallerScope == "AppUser:" + f.Actor.ToString("D"), Ct));
    }

    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) => eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added)
                ? throw new InvalidOperationException("Test audit failure") : ValueTask.FromResult(result);
    }

    private sealed class ShiftHost(WebApplication app) : IAsyncDisposable
    {
        public WebApplication App => app;
        public Guid Actor { get; } = Guid.NewGuid(); public Guid Other { get; } = Guid.NewGuid();
        public Guid Patient { get; } = Guid.NewGuid(); private Guid PriceId { get; } = Guid.NewGuid();
        private Guid PriceItemId { get; } = Guid.NewGuid();
        public static async Task<ShiftHost> CreateAsync(Action<IServiceCollection>? configure = null)
        {
            var cs = TestDatabase.RequiredConnectionString();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Shift.Tests";
            builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs)
                .AddBillingInfrastructure(cs).AddBillingApplication().AddBillingApi().AddUnitOfWorkParticipant<BillingDbContext>();
            configure?.Invoke(builder.Services);
            var app = builder.Build(); app.MapBillingEndpoints(); await app.StartAsync(Ct); return new(app);
        }
        public HttpClient Client(Guid? actor = null, string role = Roles.Receptionist, string channel = Channels.Staff, bool authenticate = true)
        {
            var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            if (authenticate)
            {
                var token = new JwtSecurityToken("Shift.Tests", "Shift.Tests", [new("sub", (actor ?? Actor).ToString()), new("role", role), new(AppClaims.Channel, channel)],
                    expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(app.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            }
            return client;
        }
        public async Task<HttpResponseMessage> CloseAsync(HttpClient client, decimal amount, string? note, string? key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(new { actualCashCounted = amount, discrepancyNote = note }) };
            if (key is not null) request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request, Ct);
        }
        public Task<HttpResponseMessage> CollectAsync(HttpClient client, Guid order) =>
            client.PostAsJsonAsync($"/api/billing/staff/cashier/cls-batches/{order}/collect-cash", new { }, Ct);
        public async Task<(Guid Order, Guid Charge)> BatchAsync(decimal amount)
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var now = await scope.ServiceProvider.GetRequiredService<IDbClock>().GetUtcNowAsync(Ct);
            if (!await db.Set<PriceList>().AnyAsync(p => p.Id == PriceId, Ct))
            {
                db.Add(new PriceList { Id = PriceId, PriceListCode = PriceId.ToString(), PriceListName = "Shift test", Currency = "VND", EffectiveFromUtc = DateTime.UtcNow.AddDays(-1), IsActive = true });
                db.Add(new PriceListItem { Id = PriceItemId, PriceListId = PriceId, ItemType = ItemType.CLSService, MedicalServiceId = Guid.NewGuid(), IsActive = true });
            }
            var order = Guid.NewGuid();
            var charge = BillingItem.CreatePending(PriceItemId, ItemType.CLSService, "Shift test CLS", amount, 1, now,
                encounterId: Guid.NewGuid(), serviceOrderItemId: Guid.NewGuid(), serviceOrderId: order, patientId: Patient);
            db.Add(charge); await db.SaveChangesAsync(Ct); return (order, charge.Id);
        }
        public async Task<Guid> RefundAsync(Guid chargeId)
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var transaction = await (from t in db.Set<PaymentTransaction>() join a in db.Set<PaymentAllocation>() on t.Id equals a.PaymentTransactionId
                                      where a.BillingItemId == chargeId select t.Id).SingleAsync(Ct);
            var refund = new RefundRequest { Id = Guid.NewGuid(), BillingItemId = chargeId, OriginalPaymentTransactionId = transaction,
                RequestedAmount = 20m, RefundReason = RefundReason.SystemError, PolicyRuleApplied = PolicyRuleApplied.CustomApproval,
                Status = RefundRequestStatus.Approved, RequestedAtUtc = DateTime.UtcNow, DecidedAtUtc = DateTime.UtcNow, DecisionByStaffId = Guid.NewGuid() };
            db.Add(refund); await db.SaveChangesAsync(Ct); return refund.Id;
        }
        public async ValueTask DisposeAsync()
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            var actorIds = new[] { Actor, Other };
            var intents = await db.Set<PaymentIntent>().Where(i => i.PatientId == Patient).Select(i => i.Id).ToArrayAsync(Ct);
            var trans = await db.Set<PaymentTransaction>().Where(t => intents.Contains(t.PaymentIntentId)).Select(t => t.Id).ToArrayAsync(Ct);
            var refunds = await db.Set<RefundRequest>().Where(r => trans.Contains(r.OriginalPaymentTransactionId)).Select(r => r.Id).ToArrayAsync(Ct);
            var refundTrans = await db.Set<RefundTransaction>().Where(r => refunds.Contains(r.RefundRequestId)).Select(r => r.Id).ToArrayAsync(Ct);
            var closures = await db.Set<CashierShiftClosure>().Where(c => actorIds.Contains(c.CashierUserId)).Select(c => c.Id).ToArrayAsync(Ct);
            await db.Set<RefundTransaction>().Where(r => refunds.Contains(r.RefundRequestId)).ExecuteDeleteAsync(Ct);
            await db.Set<RefundRequest>().Where(r => refunds.Contains(r.Id)).ExecuteDeleteAsync(Ct);
            await db.Set<PaymentAllocation>().Where(a => trans.Contains(a.PaymentTransactionId)).ExecuteDeleteAsync(Ct);
            await db.Set<PaymentTransaction>().Where(t => trans.Contains(t.Id)).ExecuteDeleteAsync(Ct);
            await db.Set<PaymentIntent>().Where(i => intents.Contains(i.Id)).ExecuteDeleteAsync(Ct);
            await db.Set<BillingItem>().Where(i => i.PriceListItemId == PriceItemId).ExecuteDeleteAsync(Ct);
            await db.Set<PriceListItem>().Where(i => i.Id == PriceItemId).ExecuteDeleteAsync(Ct);
            await db.Set<PriceList>().Where(i => i.Id == PriceId).ExecuteDeleteAsync(Ct);
            await db.Set<CashierShiftClosure>().Where(c => actorIds.Contains(c.CashierUserId)).ExecuteDeleteAsync(Ct);
            await shared.Set<AuditLog>().Where(a => closures.Contains(a.RecordId) || trans.Contains(a.RecordId) || refundTrans.Contains(a.RecordId)).ExecuteDeleteAsync(Ct);
            await shared.Set<OutboxMessage>().Where(o => intents.Contains(o.AggregateId)).ExecuteDeleteAsync(Ct);
            await shared.Set<ApiIdempotencyRecord>().Where(r => r.CallerScope == "AppUser:" + Actor.ToString("D") || r.CallerScope == "AppUser:" + Other.ToString("D")).ExecuteDeleteAsync(Ct);
            await app.StopAsync(Ct); await app.DisposeAsync();
        }
    }
}
