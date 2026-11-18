using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Billing.Api;
using Billing.Application;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure;
using Billing.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Abstractions;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(BillingPaymentSerialCollection))]
public sealed class AdminPriceListTests
{
    private const string Route = "/api/billing/admin/price-lists";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_prices_require_admin_staff_for_both_methods()
    {
        await using var f = await PriceHost.CreateAsync();
        using var anonymous = f.Client(authenticate: false);
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            using var request = new HttpRequestMessage(method, Route) { Content = method == HttpMethod.Post ? JsonContent.Create(f.Body()) : null };
            using var response = await anonymous.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            foreach (var (role, channel) in new[] { (Roles.Admin, Channels.Patient), (Roles.Patient, Channels.Patient),
                (Roles.Doctor, Channels.Staff), (Roles.Nurse, Channels.Staff), (Roles.Receptionist, Channels.Staff), (Roles.RoomAccount, Channels.Staff) })
            {
                using var wrong = f.Client(role, channel);
                using var attempt = new HttpRequestMessage(method, Route) { Content = method == HttpMethod.Post ? JsonContent.Create(f.Body()) : null };
                using var denied = await wrong.SendAsync(attempt, Ct);
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Creates_and_lists_rank_prices_with_local_dates_and_optional_idempotency()
    {
        await using var f = await PriceHost.CreateAsync();
        using var client = f.Client();
        var body = f.Body();
        using var created = await f.PostAsync(client, body, "create");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("2085-01-01", json.GetProperty("effectiveFrom").GetString());
        Assert.Equal("Test rank", json.GetProperty("consultationPrices")[0].GetProperty("rankName").GetString());
        f.RankSource.Active = false;
        using var replay = await f.PostAsync(client, body, "create");
        Assert.Equal(await created.Content.ReadAsStringAsync(Ct), await replay.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        body["name"] = "Different payload";
        using var conflict = await f.PostAsync(client, body, "create");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var listed = await client.GetAsync(Route + "?page=1&pageSize=100", Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains((await listed.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == json.GetProperty("id").GetGuid());
        await using var scope = f.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var row = await db.Set<PriceList>().SingleAsync(x => x.Id == json.GetProperty("id").GetGuid(), Ct);
        Assert.Equal(new DateTime(2084, 12, 31, 17, 0, 0, DateTimeKind.Utc), row.EffectiveFromUtc);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().CountAsync(x => x.RecordId == row.Id, Ct));
    }

    [Fact]
    public async Task In_progress_idempotency_is_409_without_creating_a_list()
    {
        await using var f = await PriceHost.CreateAsync(services => services.AddScoped<IIdempotencyStore, BusyKey>());
        using var client = f.Client();
        using var response = await f.PostAsync(client, f.Body(), "busy");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("RequestInProgress", await response.Content.ReadAsStringAsync(Ct));
    }

    private sealed class BusyKey : IIdempotencyStore
    {
        public Task<IdempotencyBeginResult> BeginAsync(string callerScope, string operationType, string key, string requestHash,
            TimeSpan ttl, Guid? operationId, CancellationToken ct) => Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.InProgress(null));
        public Task CompleteAsync(string callerScope, string operationType, string key, int statusCode, string? bodyJson,
            Guid? operationId, CancellationToken ct) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Overlap_is_409_adjacent_is_valid_and_paid_snapshot_remains_unchanged()
    {
        await using var f = await PriceHost.CreateAsync();
        using var client = f.Client();
        using var first = await f.PostAsync(client, f.Body("first"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var id = (await first.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        await using var scope = f.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var item = await db.Set<PriceListItem>().SingleAsync(x => x.PriceListId == id, Ct);
        var charge = BillingItem.CreatePending(item.Id, ItemType.ConsultationFee, "Paid snapshot", 100m, 1, DateTime.UtcNow);
        charge.PaidAmount = 100m; charge.Status = BillingItemStatus.Paid;
        db.Add(charge); await db.SaveChangesAsync(Ct);
        var overlap = f.Body("overlap"); overlap["effectiveFrom"] = "2085-01-14";
        using var rejected = await f.PostAsync(client, overlap);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        var next = f.Body("next"); next["effectiveFrom"] = "2085-01-15"; next["effectiveTo"] = null;
        next["consultationPrices"]![0]!["price"] = 200m;
        using var adjacent = await f.PostAsync(client, next);
        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
        var catalog = scope.ServiceProvider.GetRequiredService<IPriceCatalog>();
        var at = new DateTime(2085, 1, 14, 17, 0, 0, DateTimeKind.Utc);
        var quote = PriceResolver.ResolveExamFee(await catalog.GetConsultationCandidatesAsync(Guid.NewGuid(), f.RankId, at, Ct), Guid.NewGuid(), f.RankId, at);
        Assert.Equal(200m, quote.UnitPrice);
        await db.Entry(charge).ReloadAsync(Ct);
        Assert.Equal((100m, 100m, 100m), (charge.UnitPrice, charge.TotalAmount, charge.PaidAmount));
    }

    [Fact]
    public async Task Parallel_overlapping_creates_only_one_price_list()
    {
        await using var f = await PriceHost.CreateAsync();
        using var first = f.Client(); using var second = f.Client();
        var responses = await Task.WhenAll(f.PostAsync(first, f.Body("race1")), f.PostAsync(second, f.Body("race2")));
        try { Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, responses.Select(r => r.StatusCode).OrderBy(x => (int)x)); }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("code", "")]
    [InlineData("name", "")]
    [InlineData("effectiveFrom", "invalid")]
    [InlineData("effectiveFrom", "0001-01-01")]
    [InlineData("effectiveTo", "2084-01-01")]
    [InlineData("rank", "unknown")]
    [InlineData("prices", "empty")]
    [InlineData("price", "-1")]
    [InlineData("price", "1.001")]
    [InlineData("rank", "duplicate")]
    public async Task Invalid_price_payload_is_400(string field, string value)
    {
        await using var f = await PriceHost.CreateAsync(); using var client = f.Client();
        var body = f.Body();
        if (field == "price") body["consultationPrices"]![0]!["price"] = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        else if (field == "rank" && value == "duplicate") body["consultationPrices"]!.AsArray().Add(body["consultationPrices"]![0]!.DeepClone());
        else if (field == "rank") body["consultationPrices"]![0]!["rankName"] = value;
        else if (field == "prices") body["consultationPrices"] = new JsonArray();
        else body[field] = value;
        using var response = await f.PostAsync(client, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bad_transport_keys_and_paging_are_rejected_and_duplicate_code_is_409()
    {
        await using var f = await PriceHost.CreateAsync(); using var client = f.Client();
        foreach (var content in new[] { new StringContent("{" , Encoding.UTF8, "application/json"), new StringContent("null", Encoding.UTF8, "application/json") })
        {
            using (content) { using var response = await client.PostAsync(Route, content, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        }
        using var plain = await client.PostAsync(Route, new StringContent("plain"), Ct);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, plain.StatusCode);
        using var badKey = await f.PostAsync(client, f.Body(), "bad key");
        Assert.Equal(HttpStatusCode.BadRequest, badKey.StatusCode);
        foreach (var query in new[] { "?page=0", "?page=1001", "?pageSize=0", "?pageSize=101" })
        { using var response = await client.GetAsync(Route + query, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        using var first = await f.PostAsync(client, f.Body()); Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await f.PostAsync(client, f.Body()); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    public class Ranks : DispatchProxy
    {
        public Guid RankId { get; set; }
        public bool Active { get; set; } = true;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name == nameof(IIdentityPatientsModule.GetDoctorRanksAsync)
            ? Task.FromResult<IReadOnlyList<DoctorRankDto>>(Active ? [new(RankId, "Test", "Test rank", 0)] : []) : throw new NotSupportedException();
    }

    private sealed class PriceHost(WebApplication app, Guid rankId, Ranks rankSource) : IAsyncDisposable
    {
        public WebApplication App => app;
        public Guid RankId => rankId;
        public Ranks RankSource => rankSource;
        public Guid UserId { get; } = Guid.NewGuid();
        public string Prefix { get; } = "pl-" + Guid.NewGuid().ToString("N");
        public JsonObject Body(string suffix = "one") => new() { ["code"] = Prefix + suffix, ["name"] = "Test prices", ["effectiveFrom"] = "2085-01-01",
            ["effectiveTo"] = "2085-01-15", ["isActive"] = true, ["consultationPrices"] = new JsonArray(new JsonObject { ["rankName"] = "Test rank", ["price"] = 100m }) };
        public static async Task<PriceHost> CreateAsync(Action<IServiceCollection>? configure = null)
        {
            var cs = TestDatabase.RequiredConnectionString();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Price.Tests";
            builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs)
                .AddBillingInfrastructure(cs).AddBillingApi().AddUnitOfWorkParticipant<BillingDbContext>();
            var identity = DispatchProxy.Create<IIdentityPatientsModule, Ranks>(); var rank = Guid.NewGuid(); ((Ranks)(object)identity).RankId = rank;
            builder.Services.AddSingleton(identity);
            configure?.Invoke(builder.Services);
            var app = builder.Build(); app.MapBillingEndpoints(); await app.StartAsync(Ct); return new(app, rank, (Ranks)(object)identity);
        }
        public HttpClient Client(string role = Roles.Admin, string channel = Channels.Staff, bool authenticate = true)
        {
            var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            if (authenticate)
            {
                var token = new JwtSecurityToken("Price.Tests", "Price.Tests", [new("sub", UserId.ToString()), new("role", role), new(AppClaims.Channel, channel)],
                    expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(app.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            }
            return client;
        }
        public async Task<HttpResponseMessage> PostAsync(HttpClient client, JsonObject body, string? key = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(body) };
            if (key is not null) request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request, Ct);
        }
        public async ValueTask DisposeAsync()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>(); var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            var ids = await db.Set<PriceList>().Where(p => p.PriceListCode.StartsWith(Prefix)).Select(p => p.Id).ToArrayAsync(Ct);
            var itemIds = await db.Set<PriceListItem>().Where(p => ids.Contains(p.PriceListId)).Select(p => p.Id).ToArrayAsync(Ct);
            await db.Set<BillingItem>().Where(x => itemIds.Contains(x.PriceListItemId)).ExecuteDeleteAsync(Ct);
            await db.Set<PriceListItem>().Where(x => ids.Contains(x.PriceListId)).ExecuteDeleteAsync(Ct);
            await db.Set<PriceList>().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(Ct);
            await shared.Set<AuditLog>().Where(x => ids.Contains(x.RecordId)).ExecuteDeleteAsync(Ct);
            await shared.Set<ApiIdempotencyRecord>().Where(x => x.CallerScope == "AppUser:" + UserId.ToString("D")).ExecuteDeleteAsync(Ct);
            await app.StopAsync(Ct); await app.DisposeAsync();
        }
    }
}
