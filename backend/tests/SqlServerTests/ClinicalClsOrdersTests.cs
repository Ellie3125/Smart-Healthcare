using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Billing.Application;
using Billing.Domain.Entities;
using Billing.Infrastructure;
using Billing.Infrastructure.Persistence;
using Clinical.Api;
using Clinical.Application.Orders;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Scheduling.Application;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Infrastructure;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClinicalClsOrdersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Closed_missing_encounter_key_conflicts_and_transport_errors_have_named_responses()
    {
        await using var f = await ClsOrderFixture.CreateAsync();
        using var client = f.Client();
        using var noKey = await client.PostAsJsonAsync(f.Route, new { serviceIds = new[] { f.ServiceId } }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        using var text = await client.PostAsync(f.Route, new StringContent("plain"), Ct);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, text.StatusCode);
        foreach (var json in new[] { "null", "{", "{}", "{\"serviceIds\":[\"bad-guid\"]}" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, f.Route) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            request.Headers.Add("Idempotency-Key", "invalid");
            using var invalid = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var missingRequest = new HttpRequestMessage(HttpMethod.Post, f.Route.Replace(f.EncounterId.ToString(), Guid.NewGuid().ToString()))
            { Content = JsonContent.Create(new { serviceIds = new[] { f.ServiceId } }) };
        missingRequest.Headers.Add("Idempotency-Key", "missing");
        using var missing = await client.SendAsync(missingRequest, Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var saved = await f.OrderAsync(client, "conflict");
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        using var conflict = await f.OrderAsync(client, "conflict", [f.ServiceId, Guid.NewGuid()]);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("IdempotencyConflict", await conflict.Content.ReadAsStringAsync(Ct));
        await using var scope = f.App.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IClsOrderStore>().LockEncounterAsync(f.EncounterId, Ct));
        var directory = scope.ServiceProvider.GetRequiredService<Scheduling.Contracts.IClsServiceDirectory>();
        await Assert.ThrowsAsync<Scheduling.Contracts.SchedulingException>(() => directory.GetAsync([Guid.Empty], Ct));
        foreach (var status in new[] { EncounterStatus.Completed, EncounterStatus.Abandoned })
        {
            await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<Encounter>().Where(x => x.Id == f.EncounterId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status), Ct);
            using var closed = await f.OrderAsync(client);
            Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
            Assert.Contains("EncounterClosed", await closed.Content.ReadAsStringAsync(Ct));
        }
        Assert.Equal((1, 1, 1, 2), await f.CountsAsync());
    }

    [Fact]
    public async Task In_progress_order_never_creates_a_second_batch()
    {
        await using var f = await ClsOrderFixture.CreateAsync(s => s.AddScoped<IIdempotencyStore, InProgress>());
        using var client = f.Client();
        using var response = await f.OrderAsync(client);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("RequestInProgress", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal((0, 0, 0, 0), await f.CountsAsync());
    }

    private sealed class InProgress : IIdempotencyStore
    {
        public Task<IdempotencyBeginResult> BeginAsync(string a, string o, string k, string h, TimeSpan ttl, Guid? operation, CancellationToken ct) =>
            Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.InProgress(null));
        public Task CompleteAsync(string a, string o, string k, int status, string? body, Guid? operation, CancellationToken ct) => throw new InvalidOperationException();
    }

    [Fact]
    public async Task Real_billing_and_scheduling_commit_once_and_replay_the_same_batch()
    {
        await using var f = await ClsOrderFixture.CreateAsync();
        using var client = f.Client();
        var responses = await Task.WhenAll(f.OrderAsync(client, "same"), f.OrderAsync(client, "same"));
        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
            Assert.Equal(await responses[0].Content.ReadAsStringAsync(Ct), await responses[1].Content.ReadAsStringAsync(Ct));
            var body = await responses[0].Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(123456m, body.GetProperty("order").GetProperty("items")[0].GetProperty("appliedPrice").GetDecimal());
        }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Equal((1, 1, 1, 2), await f.CountsAsync());
        await using var scope = f.App.Services.CreateAsyncScope();
        var charge = await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Set<BillingItem>().SingleAsync(x => x.EncounterId == f.EncounterId, Ct);
        Assert.Equal(f.PatientId, charge.PatientId);
        Assert.NotNull(charge.ServiceOrderId);
        Assert.Equal("Test service", charge.ItemNameSnapshot);

        // Encounter đang WaitingCLS: khóa mới không mở đợt thứ hai, không thêm vé quay lại.
        using var second = await f.OrderAsync(client, "second");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("ClsBatchInProgress", await second.Content.ReadAsStringAsync(Ct));
        Assert.Equal((1, 1, 1, 2), await f.CountsAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Billing_or_scheduling_error_rolls_back_order_items_fees_and_tickets(bool billingFailure)
    {
        await using var f = await ClsOrderFixture.CreateAsync();
        await using (var scope = f.App.Services.CreateAsyncScope())
        {
            if (billingFailure) await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Set<PriceListItem>()
                .Where(x => x.Id == f.PriceItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
            else await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>()
                .Where(x => x.Id == f.InitialTicketId).ExecuteDeleteAsync(Ct);
        }
        using var client = f.Client();
        using var response = await f.OrderAsync(client);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(billingFailure ? "PriceNotConfigured" : "InitialTicketNotFound", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal((0, 0, 0, 0), await f.CountsAsync());
        await using var check = f.App.Services.CreateAsyncScope();
        Assert.Equal(EncounterStatus.InProgress, (await check.ServiceProvider.GetRequiredService<ClinicalDbContext>()
            .Set<Encounter>().SingleAsync(x => x.Id == f.EncounterId, Ct)).Status);
        Assert.False(await check.ServiceProvider.GetRequiredService<SharedDbContext>().Set<ApiIdempotencyRecord>()
            .AnyAsync(x => x.CallerScope == "AppUser:" + f.UserId.ToString("D"), Ct));
    }

    [Fact]
    public async Task New_route_rejects_anonymous_wrong_role_channel_doctor_and_invalid_services()
    {
        await using var f = await ClsOrderFixture.CreateAsync();
        using var anonymous = f.Client(authenticate: false);
        using var unauthenticated = await f.OrderAsync(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Doctor, Channels.Patient), (Roles.Patient, Channels.Patient),
            (Roles.Nurse, Channels.Staff), (Roles.RoomAccount, Channels.Staff), (Roles.Admin, Channels.Staff), (Roles.Receptionist, Channels.Staff) })
        {
            using var wrong = f.Client(role, channel);
            using var denied = await f.OrderAsync(wrong);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var other = f.Client(doctor: Guid.NewGuid());
        using var forbidden = await f.OrderAsync(other);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var client = f.Client();
        foreach (var ids in new[] { Array.Empty<Guid>(), new[] { Guid.Empty }, new[] { f.ServiceId, f.ServiceId }, Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray() })
        {
            using var invalid = await f.OrderAsync(client, ids: ids);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var unavailable = await f.OrderAsync(client, ids: [Guid.NewGuid()]);
        Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        Assert.Contains("MedicalServiceUnavailable", await unavailable.Content.ReadAsStringAsync(Ct));
        Assert.Equal((0, 0, 0, 0), await f.CountsAsync());
    }
}

internal sealed class ClsOrderFixture(WebApplication app) : IAsyncDisposable
{
    public WebApplication App { get; } = app;
    public Guid EncounterId { get; } = Guid.NewGuid();
    public Guid UserId { get; } = Guid.NewGuid();
    public Guid DoctorId { get; } = Guid.NewGuid();
    public Guid PatientId { get; } = Guid.NewGuid();
    public Guid ServiceId { get; } = Guid.NewGuid();
    public Guid RoomId { get; } = Guid.NewGuid();
    public Guid InitialTicketId { get; } = Guid.NewGuid();
    public Guid PriceItemId { get; } = Guid.NewGuid();
    private Guid PriceListId { get; } = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public string Route => $"/api/clinical/staff/doctor/encounters/{EncounterId}/cls-orders";

    public static async Task<ClsOrderFixture> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var cs = TestDatabase.RequiredConnectionString();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "Cls.Orders.Tests";
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, cs).AddSharedInfrastructure(cs)
            .AddBillingApplication().AddBillingInfrastructure(cs).AddSchedulingApplication().AddSchedulingInfrastructure(cs)
            .AddClinicalInfrastructure(cs).AddClinicalApi().AddUnitOfWorkParticipant<ClinicalDbContext>()
            .AddUnitOfWorkParticipant<SchedulingDbContext>().AddUnitOfWorkParticipant<BillingDbContext>()
            .AddScoped<Scheduling.Application.DoctorSearch.IDoctorSearchDirectory, Scheduling.Application.DoctorSearch.IdentityDoctorSearchDirectory>()
            .AddSingleton(DispatchProxy.Create<IIdentityPatientsModule, RoomDirectory>());
        configure?.Invoke(builder.Services);
        var app = builder.Build(); app.MapClinicalEndpoints(); await app.StartAsync(Ct);
        var f = new ClsOrderFixture(app);
        await using var scope = app.Services.CreateAsyncScope();
        var clinical = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        clinical.Add(new Encounter { Id = f.EncounterId, AppointmentId = Guid.NewGuid(), PatientId = f.PatientId, OriginalDoctorId = f.DoctorId,
            OriginalRoomId = f.RoomId, SpecialtySnapshot = "Test", DoctorRankSnapshot = "Test", CheckedInAtUtc = DateTime.UtcNow, Status = EncounterStatus.InProgress });
        await clinical.SaveChangesAsync(Ct);
        var scheduling = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        scheduling.Add(new MedicalService { Id = f.ServiceId, ServiceCode = f.ServiceId.ToString("N"), ServiceName = "Test service",
            ServiceCategory = ServiceCategory.Laboratory, DefaultRoomId = f.RoomId, IsActive = true, IsMandatoryDefault = true });
        scheduling.Add(new MedicalServiceRoom { Id = Guid.NewGuid(), MedicalServiceId = f.ServiceId, RoomId = f.RoomId, IsDefaultRoom = true, IsActive = true });
        scheduling.Add(new QueueTicket { Id = f.InitialTicketId, EncounterId = f.EncounterId, TargetRoomId = f.RoomId, TicketType = TicketType.InitialConsultation,
            TicketNumber = $"{f.EncounterId:N}-001", SequenceNumber = 1, QueueStatus = QueueStatus.InProgress, IssuedAtUtc = DateTime.UtcNow });
        await scheduling.SaveChangesAsync(Ct);
        var billing = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        billing.Add(new PriceList { Id = f.PriceListId, PriceListCode = f.PriceListId.ToString("N"), PriceListName = "Test", Currency = "VND", IsActive = true, EffectiveFromUtc = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        billing.Add(new PriceListItem { Id = f.PriceItemId, PriceListId = f.PriceListId, MedicalServiceId = f.ServiceId, ItemType = Billing.Domain.Enums.ItemType.CLSService, UnitPrice = 123456m, IsActive = true });
        await billing.SaveChangesAsync(Ct);
        return f;
    }
    public HttpClient Client(string role = Roles.Doctor, string channel = Channels.Staff, Guid? doctor = null, bool authenticate = true)
    {
        var client = new HttpClient { BaseAddress = new(App.Urls.Single()) };
        if (authenticate)
        {
            var token = new JwtSecurityToken("Cls.Orders.Tests", "Cls.Orders.Tests", [new("sub", UserId.ToString()), new("role", role),
                new(AppClaims.Channel, channel), new(AppClaims.DoctorId, (doctor ?? DoctorId).ToString())], expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(App.Configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return client;
    }
    public async Task<HttpResponseMessage> OrderAsync(HttpClient client, string? key = null, Guid[]? ids = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(new { serviceIds = ids ?? [ServiceId], patientId = Guid.NewGuid(), appliedPrice = 1 }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request, Ct);
    }
    public async Task<(int Orders, int Items, int Fees, int Tickets)> CountsAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var clinical = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        var ids = clinical.Set<ServiceOrder>().Where(x => x.EncounterId == EncounterId).Select(x => x.Id);
        return (await ids.CountAsync(Ct), await clinical.Set<ServiceOrderItem>().CountAsync(x => ids.Contains(x.ServiceOrderId), Ct),
            await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Set<BillingItem>().CountAsync(x => x.EncounterId == EncounterId, Ct),
            await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>().CountAsync(x => x.EncounterId == EncounterId && x.Id != InitialTicketId, Ct));
    }
    public async ValueTask DisposeAsync()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            await shared.Set<ApiIdempotencyRecord>().Where(x => x.CallerScope == "AppUser:" + UserId.ToString("D")).ExecuteDeleteAsync(Ct);
            await shared.Set<AuditLog>().Where(x => x.PerformedByUserId == UserId).ExecuteDeleteAsync(Ct);
            var billing = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            await billing.Set<BillingItem>().Where(x => x.EncounterId == EncounterId).ExecuteDeleteAsync(Ct);
            await billing.Set<PriceListItem>().Where(x => x.Id == PriceItemId).ExecuteDeleteAsync(Ct);
            await billing.Set<PriceList>().Where(x => x.Id == PriceListId).ExecuteDeleteAsync(Ct);
            var scheduling = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            await scheduling.Set<QueueTicket>().Where(x => x.EncounterId == EncounterId).ExecuteDeleteAsync(Ct);
            await scheduling.Set<MedicalServiceRoom>().Where(x => x.MedicalServiceId == ServiceId).ExecuteDeleteAsync(Ct);
            await scheduling.Set<MedicalService>().Where(x => x.Id == ServiceId).ExecuteDeleteAsync(Ct);
            var clinical = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
            var ids = clinical.Set<ServiceOrder>().Where(x => x.EncounterId == EncounterId).Select(x => x.Id);
            await clinical.Set<ServiceOrderItem>().Where(x => ids.Contains(x.ServiceOrderId)).ExecuteDeleteAsync(Ct);
            await clinical.Set<ServiceOrder>().Where(x => x.EncounterId == EncounterId).ExecuteDeleteAsync(Ct);
            await clinical.Set<Encounter>().Where(x => x.Id == EncounterId).ExecuteDeleteAsync(Ct);
        }
        await App.StopAsync(Ct); await App.DisposeAsync();
    }
    public class RoomDirectory : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == nameof(IIdentityPatientsModule.GetRoomAsync)
            ? Task.FromResult<RoomDto?>(new((Guid)args![0]!, "Test", "Test", "CLS", true)) : throw new NotSupportedException(method.Name);
    }
}
