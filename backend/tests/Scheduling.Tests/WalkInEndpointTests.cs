using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Billing.Contracts;
using IdentityPatients.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Scheduling.Api;
using Scheduling.Application.BookingAttempts;
using Scheduling.Application.Holds;
using Scheduling.Application.WalkIn;
using Shared.Abstractions;
using Xunit;

namespace Scheduling.Tests;

public sealed class WalkInEndpointTests
{
    [Fact]
    public async Task WalkIn_requires_receptionist_staff_and_valid_ids_and_idempotency_header()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddScoped<BookingAttemptService>().AddScoped<HoldService>()
            .AddSingleton(DispatchProxy.Create<IBookingAttemptStore, CheckInTestServices.UnusedContract>())
            .AddSingleton(DispatchProxy.Create<IHoldStore, CheckInTestServices.UnusedContract>())
            .AddSingleton(DispatchProxy.Create<IBookingPaymentStore, CheckInTestServices.UnusedContract>())
            .AddSingleton(DispatchProxy.Create<ICounterExamFeeCollector, CheckInTestServices.UnusedContract>())
            .AddSingleton(DispatchProxy.Create<IWalkInStore, CheckInTestServices.UnusedContract>())
            .AddSingleton(DispatchProxy.Create<IIntegrationEventHandler<PaymentVerified>, CheckInTestServices.UnusedContract>())
            .AddSingleton(DispatchProxy.Create<IIdempotencyStore, CheckInTestServices.UnusedContract>())
            .AddSingleton(DispatchProxy.Create<IOperationTracker, CheckInTestServices.UnusedContract>())
            .AddSingleton(DispatchProxy.Create<IWalkInPatientDirectory, CheckInTestServices.UnusedContract>());
        await using var app = builder.Build();
        app.MapSchedulingWalkInEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        const string route = "/api/scheduling/staff/walk-in";
        var body = new { PatientId = Guid.Empty, DoctorId = Guid.NewGuid() };
        using var anonymous = await client.PostAsJsonAsync(route, body, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.Admin, Channels.Staff),
            (Roles.Nurse, Channels.Staff), (Roles.Receptionist, Channels.Patient) })
        {
            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, role, channel: channel);
            using var forbidden = await client.PostAsJsonAsync(route, body, ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Receptionist);
        using var noKey = await client.PostAsJsonAsync(route, body, ct);
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var emptyPatient = await client.PostAsJsonAsync(route, body, ct);
        Assert.Equal(HttpStatusCode.BadRequest, emptyPatient.StatusCode);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", new string('k', 65));
        using var longKey = await client.PostAsJsonAsync(route, body, ct);
        Assert.Equal(HttpStatusCode.BadRequest, longKey.StatusCode);
    }

    [Fact]
    public async Task Reception_search_requires_receptionist_and_phone_or_national_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        var patient = new ReceptionPatientDto(Guid.NewGuid(), "Nguyễn Văn A", new DateOnly(1990, 1, 1), "Male", "*******678", null, true);
        builder.Services.AddSingleton(ReceptionIdentity.Create(patient));
        await using var app = builder.Build();
        app.MapSchedulingWalkInEndpoints();
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        const string route = "/api/scheduling/staff/reception/search?keyword=0912345678";

        using var anonymous = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var (role, channel) in new[] { (Roles.Patient, Channels.Patient), (Roles.Nurse, Channels.Staff),
            (Roles.Receptionist, Channels.Patient) })
        {
            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, role, channel: channel);
            using var forbidden = await client.GetAsync(route, ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Receptionist);
        using var tooShort = await client.GetAsync("/api/scheduling/staff/reception/search?keyword=0912", ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        var found = await client.GetFromJsonAsync<List<ReceptionPatientDto>>(route, ct);
        Assert.Equal(patient, Assert.Single(found!));
        Assert.Equal("0912345678", ReceptionIdentity.LastKeyword);
    }

    /// <summary>Identity giả: chỉ trả lời tra cứu của tiếp tân.</summary>
    public class ReceptionIdentity : DispatchProxy
    {
        public static string? LastKeyword;
        private ReceptionPatientDto[] _patients = [];

        public static IIdentityPatientsModule Create(params ReceptionPatientDto[] patients)
        {
            var proxy = DispatchProxy.Create<IIdentityPatientsModule, ReceptionIdentity>();
            ((ReceptionIdentity)(object)proxy)._patients = patients;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IIdentityPatientsModule.FindPatientsForReceptionAsync))
                throw new NotSupportedException(targetMethod?.Name);
            LastKeyword = (string?)args![0];
            return Task.FromResult<IReadOnlyList<ReceptionPatientDto>>(_patients);
        }
    }
}
