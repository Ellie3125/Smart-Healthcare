using System.Text.Json;
using System.Security.Cryptography;
using Clinical.Application.Results;
using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Clinical.Infrastructure.Persistence;
using DocumentsReporting.Application;
using DocumentsReporting.Contracts;
using DocumentsReporting.Domain.Entities;
using DocumentsReporting.Infrastructure;
using DocumentsReporting.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Contracts;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClinicalClsPublicationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Publication_replays_atomically_and_preserves_text_revision_files_and_the_real_reading_staff()
    {
        var directory = new ReadingDirectory();
        await using var f = await CreateAsync(directory);
        var draft = await SaveAsync(f);
        Assert.Equal(f.TicketId, draft.QueueTicketId);
        var fileId = await AttachAsync(f, draft.ResultId);
        var replies = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => PublishAsync(f, new(draft.Version), "publish")));
        Assert.All(replies, r => Assert.Equal(replies[0], r));
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        var result = await db.Set<CLSResult>().SingleAsync(x => x.Id == draft.ResultId, Ct);
        Assert.Equal(ResultStatus.Published, result.ResultStatus); Assert.Equal(directory.Doctors[0].StaffId, result.ApprovedByStaffId);
        Assert.Null(result.ApprovedAtUtc); Assert.Null(result.PerformedByStaffId);
        Assert.Equal(ItemStatus.Completed, (await f.ItemAsync()).ItemStatus);
        var revision = Assert.Single(await db.Set<CLSResultRevision>().Where(x => x.CLSResultId == result.Id).ToListAsync(Ct));
        Assert.Equal(1, revision.RevisionNumber); Assert.Equal(ClsResultRevisionKind.Published, revision.Kind);
        Assert.Equal("Tóm tắt giả lập", revision.ResultSummary); Assert.Equal("Chi tiết giả lập", revision.DetailedFindings);
        Assert.Equal(f.UserId, revision.ChangedByAccountId); Assert.Equal(result.PublishedAtUtc, revision.ChangedAtUtc);
        Assert.True((await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().ResultAttachments.SingleAsync(x => x.Id == fileId, Ct)).IsPublished);
        var message = Assert.Single(await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<OutboxMessage>().Where(x => x.AggregateId == result.Id).ToListAsync(Ct));
        Assert.Equal("ResultPublished", message.EventType.ToString());
        using var payload = JsonDocument.Parse(message.PayloadJson);
        Assert.Equal(f.EncounterId, payload.RootElement.GetProperty("EncounterId").GetGuid());
        Assert.Equal(new[] { "CLSResultId", "EncounterId", "EventId", "OccurredAtUtc", "OperationId", "ServiceOrderId", "ServiceOrderItemId", "Version" },
            payload.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain("Tóm tắt", message.PayloadJson);
        Assert.Equal(ClsPublicationError.ResultAlreadyPublished, (await Assert.ThrowsAsync<ClsPublicationException>(() => DraftAsync(f))).Error);
        Assert.Equal(ClsPublicationError.ResultAlreadyPublished, (await Assert.ThrowsAsync<ClsPublicationException>(() => SaveAsync(f, replies[0].Version))).Error);
        Assert.Equal(ClsPublicationError.ResultAlreadyPublished, (await Assert.ThrowsAsync<ClsPublicationException>(() => PublishAsync(f, new(replies[0].Version), "again"))).Error);
        Assert.Equal(ClsPublicationError.IdempotencyConflict, (await Assert.ThrowsAsync<ClsPublicationException>(() => PublishAsync(f, new(replies[0].Version), "publish"))).Error);
    }

    [Fact]
    public async Task Concurrent_distinct_keys_publish_one_revision_and_one_event()
    {
        await using var f = await CreateAsync(new());
        var draft = await SaveAsync(f);
        async Task<bool> Attempt(string key)
        {
            try { await PublishAsync(f, new(draft.Version), key); return true; }
            catch (ClsPublicationException error) when (error.Error == ClsPublicationError.ResultAlreadyPublished) { return false; }
        }
        var completed = await Task.WhenAll(Attempt("first"), Attempt("second"));
        Assert.Single(completed, x => x); Assert.Single(completed, x => !x);
        await using var scope = f.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<CLSResultRevision>().CountAsync(x => x.CLSResultId == draft.ResultId, Ct));
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<OutboxMessage>().CountAsync(x => x.AggregateId == draft.ResultId, Ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Outbox_or_caller_failure_rolls_back_result_revision_documents_audit_and_idempotency_and_same_key_can_retry(bool outboxFailure)
    {
        var directory = new ReadingDirectory(); var failure = new PublicationFailure { Fail = outboxFailure };
        await using var f = await CreateAsync(directory, failure);
        var draft = await SaveAsync(f);
        var file = await AttachAsync(f, draft.ResultId);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var scope = f.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(async ct =>
            {
                await scope.ServiceProvider.GetRequiredService<ClsPublicationService>().PublishAsync(f.ItemId, f.RoomId, f.UserId, new(draft.Version), "retry", ct);
                throw new InvalidOperationException("Caller failed");
            }, Ct);
        });
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
            Assert.Equal(ResultStatus.Draft, (await db.Set<CLSResult>().SingleAsync(x => x.Id == draft.ResultId, Ct)).ResultStatus);
            Assert.False(await db.Set<CLSResultRevision>().AnyAsync(x => x.CLSResultId == draft.ResultId, Ct));
            Assert.False((await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().ResultAttachments.SingleAsync(x => x.Id == file, Ct)).IsPublished);
            var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            Assert.False(await shared.Set<OutboxMessage>().AnyAsync(x => x.AggregateId == draft.ResultId, Ct));
            Assert.False(await shared.Set<ApiIdempotencyRecord>().AnyAsync(x => x.CallerScope == "AppUser:" + f.UserId.ToString("D"), Ct));
            Assert.False(await shared.Set<AuditLog>().AnyAsync(x => x.RecordId == draft.ResultId && x.ActionType == Shared.Infrastructure.Persistence.Enums.ActionType.PUBLISH, Ct));
            Assert.Equal(ItemStatus.ServiceFinished, (await f.ItemAsync()).ItemStatus);
        }
        failure.Fail = false;
        Assert.Equal("Published", (await PublishAsync(f, new(draft.Version), "retry")).ResultStatus);
    }

    [Theory]
    [InlineData("wrong-room", ClsPublicationError.Forbidden)]
    [InlineData("missing-ticket", ClsPublicationError.ResultNotFound)]
    [InlineData("missing-result", ClsPublicationError.ResultNotFound)]
    [InlineData("mismatched-encounter", ClsPublicationError.Forbidden)]
    [InlineData("called-ticket", ClsPublicationError.ServiceNotFinished)]
    [InlineData("item-in-progress", ClsPublicationError.ServiceNotFinished)]
    [InlineData("cancelled-order", ClsPublicationError.OrderNotActive)]
    [InlineData("cancelled-result", ClsPublicationError.OrderNotActive)]
    [InlineData("closed", ClsPublicationError.EncounterClosed)]
    [InlineData("abandoned", ClsPublicationError.EncounterClosed)]
    [InlineData("empty-summary", ClsPublicationError.InvalidInput)]
    [InlineData("no-doctor", ClsPublicationError.ReadingDoctorUnavailable)]
    [InlineData("invalid-doctor", ClsPublicationError.InvalidInput)]
    [InlineData("stale-version", ClsPublicationError.ConcurrencyConflict)]
    public async Task Invalid_context_or_input_never_publishes_or_retains_idempotency(string scenario, ClsPublicationError expected)
    {
        var directory = new ReadingDirectory();
        await using var f = await CreateAsync(directory);
        var draft = await SaveAsync(f); var room = f.RoomId; Guid? selected = null;
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        var scheduling = scope.ServiceProvider.GetRequiredService<Scheduling.Infrastructure.Persistence.SchedulingDbContext>();
        switch (scenario)
        {
            case "wrong-room": room = Guid.NewGuid(); break;
            case "missing-ticket": await scheduling.Set<Scheduling.Domain.Entities.QueueTicket>().Where(x => x.Id == f.TicketId).ExecuteDeleteAsync(Ct); break;
            case "missing-result": await db.Set<CLSResult>().Where(x => x.Id == draft.ResultId).ExecuteDeleteAsync(Ct); break;
            case "mismatched-encounter": await scheduling.Set<Scheduling.Domain.Entities.QueueTicket>().Where(x => x.Id == f.TicketId).ExecuteUpdateAsync(s => s.SetProperty(x => x.EncounterId, Guid.NewGuid()), Ct); break;
            case "called-ticket": await scheduling.Set<Scheduling.Domain.Entities.QueueTicket>().Where(x => x.Id == f.TicketId).ExecuteUpdateAsync(s => s.SetProperty(x => x.QueueStatus, Scheduling.Domain.Enums.QueueStatus.Called), Ct); break;
            case "item-in-progress": await db.Set<ServiceOrderItem>().Where(x => x.Id == f.ItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ItemStatus, ItemStatus.InProgress), Ct); break;
            case "cancelled-order": await db.Set<ServiceOrder>().Where(x => x.Id == f.OrderId).ExecuteUpdateAsync(s => s.SetProperty(x => x.OrderBatchStatus, OrderBatchStatus.Cancelled), Ct); break;
            case "cancelled-result": await db.Set<CLSResult>().Where(x => x.Id == draft.ResultId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ResultStatus, ResultStatus.Cancelled), Ct); break;
            case "closed": case "abandoned": await db.Set<Encounter>().Where(x => x.Id == f.EncounterId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, scenario == "closed" ? EncounterStatus.Completed : EncounterStatus.Abandoned), Ct); break;
            case "empty-summary": await db.Set<CLSResult>().Where(x => x.Id == draft.ResultId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ResultSummary, ""), Ct);
                draft = draft with { Version = Convert.ToBase64String((await db.Set<CLSResult>().AsNoTracking().SingleAsync(x => x.Id == draft.ResultId, Ct)).RowVersion) }; break;
            case "no-doctor": directory.Doctors = []; break;
            case "invalid-doctor": selected = Guid.NewGuid(); break;
            case "stale-version": draft = draft with { Version = "AAAAAAAAAAA=" }; break;
        }
        var error = await Assert.ThrowsAsync<ClsPublicationException>(() => scope.ServiceProvider.GetRequiredService<ClsPublicationService>()
            .PublishAsync(f.ItemId, room, f.UserId, new(draft.Version, selected), "rejected", Ct));
        Assert.Equal(expected, error.Error);
        Assert.False(await db.Set<CLSResultRevision>().AnyAsync(x => x.CLSResultId == draft.ResultId, Ct));
        Assert.False(await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<ApiIdempotencyRecord>()
            .AnyAsync(x => x.CallerScope == "AppUser:" + f.UserId.ToString("D"), Ct));
    }

    [Fact]
    public async Task Trust_boundary_limits_and_in_progress_idempotency_are_rejected_without_writes()
    {
        await using var f = await CreateAsync(new());
        var draft = await SaveAsync(f);
        await using var scope = f.Services.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<ClsPublicationService>();
        foreach (var version in new string?[] { null, "", "bad", "AA==" })
            Assert.Equal(ClsPublicationError.InvalidInput, (await Assert.ThrowsAsync<ClsPublicationException>(() => service.PublishAsync(f.ItemId, f.RoomId, f.UserId, new(version), "key", Ct))).Error);
        foreach (var key in new[] { "", " ", new string('k', 129), "key\n", "khóa" })
            Assert.Equal(ClsPublicationError.InvalidInput, (await Assert.ThrowsAsync<ClsPublicationException>(() => service.PublishAsync(f.ItemId, f.RoomId, f.UserId, new(draft.Version), key, Ct))).Error);
        foreach (var request in new[] { new ClsDraftRequest("", "", draft.Version), new(" ", "", draft.Version),
            new(new string('a', 4001), "", draft.Version), new("summary", new string('a', 16001), draft.Version) })
            Assert.Equal(ClsPublicationError.InvalidInput, (await Assert.ThrowsAsync<ClsPublicationException>(() => service.SaveDraftAsync(f.ItemId, f.RoomId, f.UserId, request, Ct))).Error);
        Assert.Equal(ClsPublicationError.InvalidInput, (await Assert.ThrowsAsync<ClsPublicationException>(() => service.GetDraftAsync(Guid.Empty, f.RoomId, Ct))).Error);
        Assert.Equal(ClsPublicationError.InvalidInput, (await Assert.ThrowsAsync<ClsPublicationException>(() => service.GetDraftAsync(f.ItemId, Guid.Empty, Ct))).Error);
        Assert.Equal(ClsPublicationError.InvalidInput, (await Assert.ThrowsAsync<ClsPublicationException>(() => service.PublishAsync(f.ItemId, f.RoomId, Guid.Empty, new(draft.Version), "key", Ct))).Error);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { itemId = f.ItemId, roomId = f.RoomId, Version = draft.Version, ReadingDoctorId = (Guid?)null })));
        var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        shared.Add(new ApiIdempotencyRecord { Id = Guid.NewGuid(), CallerScope = "AppUser:" + f.UserId.ToString("D"),
            OperationType = ClsPublicationService.Operation, IdempotencyKey = "busy", RequestHash = hash, CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddHours(1) });
        await shared.SaveChangesAsync(Ct);
        Assert.Equal(ClsPublicationError.RequestInProgress, (await Assert.ThrowsAsync<ClsPublicationException>(() => PublishAsync(f, new(draft.Version), "busy"))).Error);
    }

    [Fact]
    public async Task Reading_doctor_must_be_selected_from_current_shift_and_stale_versions_do_not_overwrite_a_draft()
    {
        var directory = new ReadingDirectory();
        await using var f = await CreateAsync(directory);
        var draft = await DraftAsync(f);
        directory.Doctors = [.. directory.Doctors, new(Guid.NewGuid(), Guid.NewGuid(), "Bác sĩ thứ hai")];
        Assert.Equal(ClsPublicationError.InvalidInput, (await Assert.ThrowsAsync<ClsPublicationException>(() => SaveAsync(f))).Error);
        var saved = await SaveAsync(f, doctor: directory.Doctors[1].DoctorId);
        Assert.Equal(directory.Doctors[1].DoctorId, saved.ReadingDoctorId);
        Assert.Equal(ClsPublicationError.ConcurrencyConflict, (await Assert.ThrowsAsync<ClsPublicationException>(() => SaveAsync(f, draft.Version, directory.Doctors[1].DoctorId))).Error);
        directory.Doctors = [directory.Doctors[0]];
        Assert.Equal(ClsPublicationError.ReadingDoctorUnavailable, (await Assert.ThrowsAsync<ClsPublicationException>(() => PublishAsync(f, new(saved.Version), "shift-ended"))).Error);
        Assert.Equal("Published", (await PublishAsync(f, new(saved.Version, directory.Doctors[0].DoctorId), "selected-new")).ResultStatus);
    }

    [Fact]
    public async Task Upload_in_progress_prevents_publication_and_the_key_can_retry_after_upload_completes()
    {
        await using var f = await CreateAsync(new());
        var draft = await SaveAsync(f);
        var fileId = await AttachAsync(f, draft.ResultId, uploaded: false);
        Assert.Equal(ClsPublicationError.FilesNotReady, (await Assert.ThrowsAsync<ClsPublicationException>(() => PublishAsync(f, new(draft.Version), "files"))).Error);
        await using var scope = f.Services.CreateAsyncScope();
        var documents = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var file = await documents.ResultAttachments.SingleAsync(x => x.Id == fileId, Ct);
        Assert.False(file.IsPublished);
        file.MarkUploaded(DateTime.UtcNow); await documents.SaveChangesAsync(Ct);
        Assert.Equal("Published", (await PublishAsync(f, new(draft.Version), "files")).ResultStatus);
    }

    internal static async Task<ClsResultFixture> CreateAsync(ReadingDirectory directory, PublicationFailure? failure = null)
    {
        var f = await ClsResultFixture.CreateAsync(s => Configure(s, directory, failure));
        await f.TransitionAsync(true); await f.TransitionAsync(false); return f;
    }
    internal static void Configure(IServiceCollection services, ReadingDirectory directory, PublicationFailure? failure = null)
    {
        services.AddDocumentsReportingInfrastructure(TestDatabase.RequiredConnectionString()).AddDocumentsReportingApplication()
            .AddScoped<ClsPublicationService>().AddSingleton<IIdentityPatientsModule>(directory)
            .AddScoped<ISchedulingModule>(sp => new Scheduling.Application.SchedulingModule(null!, null!,
                clsTicketReader: () => sp.GetRequiredService<IClsTicketReader>(), clinical: () => sp.GetRequiredService<IClinicalModule>()));
        if (failure is not null)
        {
            var registration = services.Single(x => x.ServiceType == typeof(IIntegrationEventPublisher));
            services.Remove(registration);
            services.AddScoped<IIntegrationEventPublisher>(sp => new FailingPublisher(
                (IIntegrationEventPublisher)ActivatorUtilities.CreateInstance(sp, registration.ImplementationType!), failure));
        }
    }
    internal static async Task<ClsResultDraft> DraftAsync(ClsResultFixture f, Guid? room = null)
    {
        await using var scope = f.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ClsPublicationService>().GetDraftAsync(f.ItemId, room ?? f.RoomId, Ct);
    }
    internal static async Task<ClsResultDraft> SaveAsync(ClsResultFixture f, string? version = null, Guid? doctor = null)
    {
        version ??= (await DraftAsync(f)).Version;
        await using var scope = f.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ClsPublicationService>().SaveDraftAsync(f.ItemId, f.RoomId, f.UserId,
            new(" Tóm tắt giả lập ", " Chi tiết giả lập ", version, doctor), Ct);
    }
    internal static async Task<ClsPublicationReceipt> PublishAsync(ClsResultFixture f, ClsPublishRequest request, string key)
    {
        await using var scope = f.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ClsPublicationService>().PublishAsync(f.ItemId, f.RoomId, f.UserId, request, key, Ct);
    }
    private static async Task<Guid> AttachAsync(ClsResultFixture f, Guid result, bool uploaded = true)
    {
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var file = ResultAttachment.CreateDraft(Guid.NewGuid(), result, f.TicketId, f.EncounterId, Guid.NewGuid(), Guid.NewGuid().ToString("N"),
            "test.pdf", "application/pdf", 1, new string('0', 64));
        if (uploaded) file.MarkUploaded(DateTime.UtcNow);
        db.Add(file); await db.SaveChangesAsync(Ct); return file.Id;
    }
    internal sealed class PublicationFailure { public bool Fail { get; set; } }
    private sealed class FailingPublisher(IIntegrationEventPublisher inner, PublicationFailure failure) : IIntegrationEventPublisher
    {
        public Task PublishAsync<T>(T e, Guid id, CancellationToken ct) where T : IIntegrationEvent =>
            failure.Fail ? throw new InvalidOperationException("Outbox unavailable") : inner.PublishAsync(e, id, ct);
    }
    internal sealed class ReadingDirectory : IIdentityPatientsModule
    {
        public bool AllowPatient { get; set; }
        public IReadOnlyList<RoomReadingDoctorDto> Doctors { get; set; } = [new(Guid.NewGuid(), Guid.NewGuid(), "Bác sĩ giả lập")];
        public Task<IReadOnlyList<RoomReadingDoctorDto>> GetRoomReadingDoctorsAsync(Guid r, DateTime at, CancellationToken ct = default) => Task.FromResult(Doctors);
        public Task<bool> CanAccessPatientAsync(Guid u, Guid p, AccessLevel l = AccessLevel.Booking, CancellationToken ct = default) => Task.FromResult(AllowPatient);
        public Task<DoctorDto?> GetDoctorAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RoomDto?> GetRoomAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? type = null, bool? active = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid id, DateOnly d, string shift, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
