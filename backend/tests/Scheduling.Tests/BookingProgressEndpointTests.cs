using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Billing.Contracts;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Scheduling.Api;
using Scheduling.Application.Appointments;
using Scheduling.Application.BookingAttempts;
using Scheduling.Application.Holds;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Shared.Abstractions;
using Xunit;

namespace Scheduling.Tests;

public sealed class BookingProgressEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Only_owner_on_patient_channel_can_read_attempt_and_operation()
    {
        var state = new State();
        await using var app = await StartAsync(state);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        foreach (var path in new[] { state.AttemptPath, state.OperationPath })
        {
            client.DefaultRequestHeaders.Authorization = null;
            using var anonymous = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Patient, state.Owner, Channels.Staff);
            using var wrongChannel = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, wrongChannel.StatusCode);

            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Receptionist, state.Owner, Channels.Patient);
            using var wrongRole = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);

            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Patient, Guid.NewGuid());
            using var otherOwner = await client.GetAsync(path, Ct);
            using var missing = await client.GetAsync(path[..(path.LastIndexOf('/') + 1)] + Guid.NewGuid().ToString("D"), Ct);
            Assert.Equal(HttpStatusCode.NotFound, otherOwner.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            using var forbiddenBody = JsonDocument.Parse(await otherOwner.Content.ReadAsStringAsync(Ct));
            using var missingBody = JsonDocument.Parse(await missing.Content.ReadAsStringAsync(Ct));
            Assert.Equal(missingBody.RootElement.GetProperty("code").GetString(),
                forbiddenBody.RootElement.GetProperty("code").GetString());

            SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Patient, state.Owner);
            using var owner = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        }

        state.HasBookingAccess = false;
        using var revoked = await client.GetAsync(state.OperationPath, Ct);
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
    }

    [Fact]
    public async Task Hold_expiring_at_database_time_requires_reselection_without_actions()
    {
        var state = new State();
        var expires = state.NowUtc.AddSeconds(1);
        state.Progress = new(state.PatientId, BookingAttemptStatus.Held, CurrentStep.SlotHeld,
            new ReservationProgress(ReservationStatus.Held, expires));
        await using var app = await StartAsync(state);
        using var client = OwnerClient(app, state);

        var before = await client.GetFromJsonAsync<BookingProgress>(state.AttemptPath, Ct);
        Assert.NotNull(before);
        Assert.Equal("Held", before.Status);
        Assert.Equal("SlotHeld", before.CurrentStep);
        Assert.Equal(expires, before.ExpiresAtUtc);
        Assert.Equal(["ContinuePayment"], before.AllowedActions);

        state.NowUtc = expires;
        var expired = await client.GetFromJsonAsync<BookingOperationProgress>(state.OperationPath, Ct);
        Assert.NotNull(expired);
        Assert.Equal("Succeeded", expired.Status);
        Assert.Equal("NeedsReselection", expired.BookingAttemptStatus);
        Assert.Equal("DoctorSelection", expired.CurrentStep);
        Assert.Equal(expires, expired.ExpiresAtUtc);
        Assert.Empty(expired.AllowedActions);
    }

    [Fact]
    public async Task Persisted_reselection_allows_another_hold()
    {
        var state = new State();
        state.Progress = new(state.PatientId, BookingAttemptStatus.NeedsReselection, CurrentStep.DoctorSelection,
            new ReservationProgress(ReservationStatus.Expired, state.NowUtc.AddSeconds(-1)));
        await using var app = await StartAsync(state);
        using var client = OwnerClient(app, state);

        var progress = await client.GetFromJsonAsync<BookingProgress>(state.AttemptPath, Ct);
        Assert.NotNull(progress);
        Assert.Equal("NeedsReselection", progress.Status);
        Assert.Equal("DoctorSelection", progress.CurrentStep);
        Assert.Equal(["Hold"], progress.AllowedActions);
    }

    [Fact]
    public async Task Replay_after_lost_response_keeps_operation_id_even_when_catalog_changes()
    {
        var state = new State();
        Guid operationId;
        await using (var first = await StartAsync(state))
        {
            using var client = OwnerClient(first, state);
            using var response = await PostHoldAsync(client, state, "hold-1");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            operationId = state.Operation.OperationId;
            Assert.Equal(state.OperationPath, response.Headers.Location?.OriginalString);
        }

        // Mở lại host, giữ fake backing store như dữ liệu bền. SQL suite kiểm replay với provider mới trên DB thật.
        state.Doctor = state.Doctor with { RankId = Guid.NewGuid(), IsActive = false };
        await using var restarted = await StartAsync(state);
        using var retryClient = OwnerClient(restarted, state);
        using var retried = await PostHoldAsync(retryClient, state, "hold-1");
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        using var json = JsonDocument.Parse(await retried.Content.ReadAsStringAsync(Ct));
        Assert.Equal(operationId, json.RootElement.GetProperty("operationId").GetGuid());
        Assert.Equal(1, state.HoldCalls);

        var operation = await retryClient.GetFromJsonAsync<BookingOperationProgress>(state.OperationPath, Ct);
        Assert.NotNull(operation);
        Assert.Equal(operationId, operation.OperationId);
        Assert.Equal("Succeeded", operation.Status);
        Assert.Equal("Held", operation.BookingAttemptStatus);
    }

    [Fact]
    public async Task Accepted_hold_returns_durable_operation_location_and_rejects_bad_requests()
    {
        var state = new State(operationPending: true);
        await using var app = await StartAsync(state);
        using var client = OwnerClient(app, state);
        using var invalid = new HttpRequestMessage(HttpMethod.Post, state.AttemptPath + "/hold")
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json"),
        };
        invalid.Headers.Add("Idempotency-Key", "hold-1");
        using var malformed = await client.SendAsync(invalid, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var problem = JsonDocument.Parse(await malformed.Content.ReadAsStringAsync(Ct));
        Assert.Equal("InvalidInput", problem.RootElement.GetProperty("code").GetString());

        using var response = await PostHoldAsync(client, state, "hold-1");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(state.OperationPath, response.Headers.Location?.OriginalString);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(state.Operation.OperationId, json.RootElement.GetProperty("operationId").GetGuid());
        Assert.Equal(0, state.HoldCalls);

        var operation = await client.GetFromJsonAsync<BookingOperationProgress>(state.OperationPath, Ct);
        Assert.NotNull(operation);
        Assert.Equal("Running", operation.Status);
    }

    [Fact]
    public async Task Create_attempt_requires_booking_access_and_replays_the_same_id()
    {
        var state = new State();
        await using var app = await StartAsync(state);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        const string route = "/api/scheduling/patient/booking-attempts";
        var body = new { state.PatientId, ServiceDate = new DateOnly(2026, 11, 13) };

        using var anonymous = await PostAsync(client, route, body, "k1");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Patient, state.Owner);
        using var noKey = await PostAsync(client, route, body, null);
        using var noPatient = await PostAsync(client, route, new { PatientId = Guid.Empty, body.ServiceDate }, "k1");
        using var stranger = await PostAsync(client, route, new { PatientId = Guid.NewGuid(), body.ServiceDate }, "k1");
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noPatient.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Empty(state.Created);

        using var created = await PostAsync(client, route, body, "k1");
        using var replay = await PostAsync(client, route, body, "k1");
        using var conflict = await PostAsync(client, route, new { state.PatientId, ServiceDate = new DateOnly(2026, 11, 14) }, "k1");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var attempt = Assert.Single(state.Created);
        Assert.Equal((state.Owner, state.PatientId, BookingAttemptStatus.Draft), (attempt.AppUserId, attempt.PatientId, attempt.Status));
        Assert.Equal($"{route}/{attempt.Id:D}", created.Headers.Location?.OriginalString);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        using var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct));
        Assert.Equal(attempt.Id, createdBody.RootElement.GetProperty("bookingAttemptId").GetGuid());
        Assert.Equal(attempt.Id, replayBody.RootElement.GetProperty("bookingAttemptId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Payment_is_created_once_for_a_live_hold_and_refused_when_it_expired()
    {
        var state = new State();
        await using var app = await StartAsync(state);
        using var client = OwnerClient(app, state);
        var route = state.AttemptPath + "/payment";

        using var notHeld = await client.PostAsync(route, null, Ct);
        Assert.Equal(HttpStatusCode.NotFound, notHeld.StatusCode);

        state.Hold = new(state.PatientId, BookingAttemptStatus.Held, state.Doctor.DoctorId, state.Doctor.RankId, Guid.NewGuid(),
            ReservationStatus.Held, state.NowUtc.AddMinutes(-1), state.NowUtc.AddMinutes(4));
        using var first = await client.PostAsync(route, null, Ct);
        using var second = await client.PostAsync(route, null, Ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(Ct), await second.Content.ReadAsStringAsync(Ct));
        using var payment = JsonDocument.Parse(await first.Content.ReadAsStringAsync(Ct));
        Assert.Equal(state.PaymentIntentId, payment.RootElement.GetProperty("paymentIntentId").GetGuid());
        Assert.Equal(150000m, payment.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(state.Hold.ExpiresAtUtc, payment.RootElement.GetProperty("expiresAtUtc").GetDateTime());
        Assert.Equal((1, 1), (state.ChargeCalls, state.PaymentCalls));
        Assert.Equal("Phí khám - BS thử", state.ChargeName);
        Assert.Equal(BookingAttemptStatus.WaitingPayment, state.Hold.AttemptStatus);

        var expired = new State();
        expired.Hold = new(expired.PatientId, BookingAttemptStatus.Held, expired.Doctor.DoctorId, expired.Doctor.RankId, Guid.NewGuid(),
            ReservationStatus.Held, expired.NowUtc.AddMinutes(-5), expired.NowUtc);
        await using var expiredApp = await StartAsync(expired);
        using var expiredClient = OwnerClient(expiredApp, expired);
        using var late = await expiredClient.PostAsync(expired.AttemptPath + "/payment", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        using var problem = JsonDocument.Parse(await late.Content.ReadAsStringAsync(Ct));
        Assert.Equal("HoldExpired", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, expired.ChargeCalls);

        SchedulingEndpointTestSetup.Authorize(expiredClient, expiredApp.Configuration, Roles.Patient, Guid.NewGuid());
        using var stranger = await expiredClient.PostAsync(expired.AttemptPath + "/payment", null, Ct);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
    }

    [Fact]
    public async Task Appointments_are_listed_for_the_booker_and_hidden_when_access_is_revoked()
    {
        var state = new State();
        var appointment = new PatientAppointment(Guid.NewGuid(), "LH-0001", AppointmentStatus.Scheduled, state.PatientId,
            state.Doctor.DoctorId, new DateOnly(2026, 11, 13), Guid.NewGuid(), 150000m, state.NowUtc, state.NowUtc.AddHours(26), null);
        state.Appointments.Add(appointment);
        await using var app = await StartAsync(state);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        const string route = "/api/scheduling/patient/appointments";

        using var anonymous = await client.GetAsync(route, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Receptionist, state.Owner);
        using var staff = await client.GetAsync(route, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);

        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Patient, state.Owner);
        using var badPage = await client.GetAsync(route + "?pageSize=51", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);

        using var list = JsonDocument.Parse(await client.GetStringAsync(route, Ct));
        Assert.False(list.RootElement.GetProperty("hasMore").GetBoolean());
        var item = Assert.Single(list.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(appointment.AppointmentId, item.GetProperty("appointmentId").GetGuid());
        Assert.Equal("LH-0001", item.GetProperty("appointmentNumber").GetString());
        Assert.Equal("Scheduled", item.GetProperty("status").GetString());
        Assert.Equal("BS thử", item.GetProperty("doctorName").GetString());
        Assert.Equal("2026-11-13", item.GetProperty("serviceDate").GetString());

        using var detail = await client.GetAsync($"{route}/{appointment.AppointmentId:D}", Ct);
        using var missing = await client.GetAsync($"{route}/{Guid.NewGuid():D}", Ct);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        state.HasBookingAccess = false;
        using var revokedList = JsonDocument.Parse(await client.GetStringAsync(route, Ct));
        using var revokedDetail = await client.GetAsync($"{route}/{appointment.AppointmentId:D}", Ct);
        Assert.Empty(revokedList.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, revokedDetail.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string route, object body, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        if (key is not null)
            request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }

    private static async Task<WebApplication> StartAsync(State state)
    {
        var builder = SchedulingEndpointTestSetup.CreateBuilder();
        builder.Services.AddSingleton<IIdentityPatientsModule>(state);
        builder.Services.AddSingleton<IDbClock>(state);
        builder.Services.AddSingleton<IBookingAttemptStore>(state);
        builder.Services.AddSingleton<IOperationTracker>(state);
        builder.Services.AddSingleton<IUnitOfWork>(state);
        builder.Services.AddSingleton<IIdempotencyStore>(state);
        builder.Services.AddSingleton<IHoldStore>(state);
        builder.Services.AddSingleton<IBookingPaymentStore>(state);
        builder.Services.AddSingleton<IPatientAppointmentReader>(state);
        builder.Services.AddSingleton<IBillingModule>(state);
        builder.Services.AddScoped<BookingProgressService>().AddScoped<HoldService>();
        builder.Services.AddScoped<BookingAttemptService>().AddScoped<BookingPaymentService>();
        var app = builder.Build();
        app.MapSchedulingEndpoints();
        await app.StartAsync(Ct);
        return app;
    }

    private static HttpClient OwnerClient(WebApplication app, State state)
    {
        var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        SchedulingEndpointTestSetup.Authorize(client, app.Configuration, Roles.Patient, state.Owner);
        return client;
    }

    private static async Task<HttpResponseMessage> PostHoldAsync(HttpClient client, State state, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, state.AttemptPath + "/hold")
        {
            Content = JsonContent.Create(new { state.Doctor.DoctorId, ServiceDate = new DateOnly(2026, 11, 12) }),
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }

    private sealed class State : IBookingAttemptStore, IOperationTracker, IIdempotencyStore, IHoldStore,
        IUnitOfWork, IDbClock, IIdentityPatientsModule, IBookingPaymentStore, IPatientAppointmentReader, IBillingModule
    {
        private string? _hash;
        private IdempotencyBeginResult.Replay? _replay;

        public State(bool operationPending = false)
        {
            Progress = new(PatientId, BookingAttemptStatus.Draft, CurrentStep.DoctorSelection, null);
            OperationPending = operationPending;
            Operation = new(Guid.NewGuid(), Owner, AttemptId, OperationType.HoldSlot, TargetEntityType.BookingAttempt,
                AttemptId, operationPending ? OperationStatus.Running : OperationStatus.Succeeded,
                null, NowUtc, operationPending ? null : NowUtc);
        }

        public Guid Owner { get; } = Guid.NewGuid();
        public Guid PatientId { get; } = Guid.NewGuid();
        public Guid AttemptId { get; } = Guid.NewGuid();
        public DateTime NowUtc { get; set; } = new(2026, 11, 12, 2, 0, 0, DateTimeKind.Utc);
        public bool HasBookingAccess { get; set; } = true;
        public bool OperationPending { get; }
        public int HoldCalls { get; private set; }
        public string AttemptPath => $"/api/scheduling/patient/booking-attempts/{AttemptId:D}";
        public string OperationPath => $"/api/scheduling/patient/operations/{Operation.OperationId:D}";
        public OperationView Operation { get; private set; }
        public BookingProgressSnapshot Progress { get; set; }
        public DoctorDto Doctor { get; set; } = new(Guid.NewGuid(), "BS thử", Guid.NewGuid(), "TEST", Guid.NewGuid(), "BS", true);

        public Task<BookingProgressSnapshot?> GetOwnedAsync(Guid bookingAttemptId, Guid appUserId, CancellationToken ct) =>
            Task.FromResult<BookingProgressSnapshot?>(bookingAttemptId == AttemptId && appUserId == Owner ? Progress : null);

        public Task<OperationView?> GetAsync(Guid operationId, CancellationToken ct) =>
            Task.FromResult<OperationView?>(operationId == Operation.OperationId ? Operation : null);

        public Task StartAsync(StartOperation operation, CancellationToken ct)
        {
            Operation = new(operation.OperationId, operation.InitiatedByUserId, operation.BookingAttemptId,
                operation.OperationType, operation.TargetEntityType, operation.TargetEntityId,
                OperationStatus.Running, null, NowUtc, null);
            return Task.CompletedTask;
        }

        public Task CompleteAsync(Guid operationId, OperationStatus status, string? failureReason, CancellationToken ct)
        {
            Operation = Operation with { Status = status, CompletedAtUtc = NowUtc };
            return Task.CompletedTask;
        }

        public Task<IdempotencyBeginResult> BeginAsync(string callerScope, string operationType, string key,
            string requestHash, TimeSpan ttl, Guid? operationId, CancellationToken ct)
        {
            if (OperationPending)
                return Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.InProgress(Operation.OperationId));
            if (_replay is not null)
                return Task.FromResult<IdempotencyBeginResult>(requestHash == _hash ? _replay : new IdempotencyBeginResult.Conflict());

            _hash = requestHash;
            return Task.FromResult<IdempotencyBeginResult>(new IdempotencyBeginResult.Started());
        }

        public Task CompleteAsync(string callerScope, string operationType, string key, int statusCode, string? bodyJson,
            Guid? operationId, CancellationToken ct)
        {
            _replay = new(statusCode, bodyJson, operationId);
            return Task.CompletedTask;
        }

        public Task<HoldOutcome> HoldAsync(HoldRequest request, TimeSpan holdDuration, CancellationToken ct)
        {
            Assert.Equal(Doctor.RankId, request.DoctorRankId);
            Assert.Equal(PatientId, request.PatientId);
            HoldCalls++;
            var expires = NowUtc + holdDuration;
            Progress = new(PatientId, BookingAttemptStatus.Held, CurrentStep.SlotHeld, new(ReservationStatus.Held, expires));
            return Task.FromResult(new HoldOutcome(HoldResult.Held, Guid.NewGuid(), expires));
        }

        public Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, AccessLevel level, CancellationToken ct) =>
            Task.FromResult(HasBookingAccess && patientId == PatientId);

        public Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct) => Task.FromResult<DoctorDto?>(Doctor);
        public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(NowUtc);
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public List<BookingAttempt> Created { get; } = [];
        public HeldBooking? Hold { get; set; }
        public Guid PaymentIntentId { get; } = Guid.NewGuid();
        public int ChargeCalls { get; private set; }
        public int PaymentCalls { get; private set; }
        public string? ChargeName { get; private set; }
        public List<PatientAppointment> Appointments { get; } = [];

        public Task AddAsync(BookingAttempt attempt, CancellationToken ct)
        {
            Created.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<HeldBooking?> GetLatestHoldAsync(Guid bookingAttemptId, Guid appUserId, CancellationToken ct) =>
            Task.FromResult(bookingAttemptId == AttemptId && appUserId == Owner ? Hold : null);

        public Task MarkWaitingPaymentAsync(Guid bookingAttemptId, CancellationToken ct)
        {
            Hold = Hold! with { AttemptStatus = BookingAttemptStatus.WaitingPayment };
            return Task.CompletedTask;
        }

        public Task<ChargeDto> CreateExamChargeAsync(CreateExamCharge command, CancellationToken ct)
        {
            Assert.Equal((Doctor.DoctorId, Doctor.RankId, Hold!.ReservedAtUtc), (command.DoctorId, command.DoctorRankId, command.PricedAtUtc));
            ChargeCalls++;
            ChargeName = command.ItemNameSnapshot;
            return Task.FromResult(new ChargeDto(Guid.NewGuid(), ChargeType.ConsultationFee, Guid.NewGuid(), command.ItemNameSnapshot,
                150000m, 1, 150000m, 0m, 0m, "VND", ChargeStatus.Pending, null, null, null));
        }

        public Task<PaymentDto> CreatePaymentAsync(CreatePayment command, CancellationToken ct)
        {
            Assert.Equal((PaymentPurpose.InitialConsultation, PatientId, Owner, (Guid?)AttemptId, PaymentProvider.VNPAY),
                (command.Purpose, command.PatientId, command.PayerAppUserId, command.BookingAttemptId, command.Provider));
            PaymentCalls++;
            return Task.FromResult(new PaymentDto(PaymentIntentId, "PI-TEST", "REF", command.Purpose, command.Provider, 150000m, "VND",
                PaymentState.Pending, command.ExpiresAtUtc, command.ChargeIds));
        }

        public Task<IReadOnlyList<PatientAppointment>> ListAsync(Guid appUserId, int offset, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PatientAppointment>>(appUserId == Owner ? Appointments.Skip(offset).Take(limit).ToList() : []);

        public Task<PatientAppointment?> GetAsync(Guid appointmentId, Guid appUserId, CancellationToken ct) =>
            Task.FromResult(appUserId == Owner ? Appointments.SingleOrDefault(x => x.AppointmentId == appointmentId) : null);

        public Task<FeeQuote> QuoteExamFeeAsync(QuoteExamFee query, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<FeeQuote>> QuoteClsFeesAsync(QuoteClsFees query, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChargeDto>> CreateClsChargesAsync(CreateClsCharges command, CancellationToken ct) => throw new NotSupportedException();
        public Task<EncounterCostSummary> GetEncounterCostSummaryAsync(Guid encounterId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> FindDueAsync(int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExpireAsync(Guid reservationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? roomType, bool? isActive, CancellationToken ct) => throw new NotSupportedException();
        public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid roomId, DateOnly date, string shiftName, CancellationToken ct) => throw new NotSupportedException();
    }
}
