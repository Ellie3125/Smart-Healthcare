using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Scheduling.Api;
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
        builder.Services.AddScoped<BookingProgressService>().AddScoped<HoldService>();
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
        IUnitOfWork, IDbClock, IIdentityPatientsModule
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
        public Task AddAsync(BookingAttempt attempt, CancellationToken ct) => throw new NotSupportedException();
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
