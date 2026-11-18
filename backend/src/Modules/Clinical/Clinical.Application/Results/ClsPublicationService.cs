using System.Security.Cryptography;
using System.Text.Json;
using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using DocumentsReporting.Contracts;
using IdentityPatients.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Clinical.Application.Results;

public sealed record ClsDraftRequest(string? SummaryText, string? TechnicalConclusion, string? Version, Guid? ReadingDoctorId = null);
public sealed record ClsPublishRequest(string? Version, Guid? ReadingDoctorId = null);
public sealed record ReadingDoctorChoice(Guid DoctorId, string FullName);
public sealed record ClsResultDraft(Guid ResultId, Guid OrderItemId, Guid QueueTicketId, string ResultStatus, string SummaryText,
    string TechnicalConclusion, string Version, Guid? ReadingDoctorId, string? ReadingDoctorName, IReadOnlyList<ReadingDoctorChoice> ReadingDoctors);
public sealed record ClsPublicationReceipt(Guid ResultId, Guid OrderItemId, string ResultStatus, string Version, DateTime PublishedAtUtc);
public enum ClsPublicationError { InvalidInput, ResultNotFound, Forbidden, EncounterClosed, ServiceNotFinished, OrderNotActive,
    ResultAlreadyPublished, ConcurrencyConflict, ReadingDoctorUnavailable, IdempotencyConflict, RequestInProgress, FilesNotReady }
public sealed class ClsPublicationException(ClsPublicationError error) : Exception(error.ToString())
{
    public ClsPublicationError Error { get; } = error;
}
public sealed record ClsResultContext(ClsItemContext Clinical, CLSResult Result, Guid QueueTicketId = default);
public interface IClsResultStore
{
    Task<ClsResultContext?> LoadAsync(Guid itemId, bool forUpdate, CancellationToken ct);
    Task SaveAsync(ClsResultContext context, Guid roomId, Guid actorId, DateTime nowUtc, bool publish, CancellationToken ct);
}

public sealed class ClsPublicationService(IClsResultStore store, IClsTicketReader tickets, IIdentityPatientsModule identity,
    IDocumentsModule documents, IUnitOfWork uow, IIdempotencyStore idempotency, IDbClock clock, IIntegrationEventPublisher publisher)
{
    public const string Operation = "Clinical.PublishClsResult";

    public Task<ClsResultDraft> GetDraftAsync(Guid itemId, Guid roomId, CancellationToken ct)
    {
        Ids(itemId, roomId);
        return uow.ExecuteAsync(async t =>
        {
            var context = await ContextAsync(itemId, roomId, false, t);
            var doctors = await identity.GetRoomReadingDoctorsAsync(roomId, await clock.GetUtcNowAsync(t), t);
            return Draft(context, doctors);
        }, ct);
    }

    public Task<ClsResultDraft> SaveDraftAsync(Guid itemId, Guid roomId, Guid actorId, ClsDraftRequest request, CancellationToken ct)
    {
        Ids(itemId, roomId, actorId);
        var version = Version(request.Version);
        var summary = request.SummaryText?.Trim(); var findings = request.TechnicalConclusion?.Trim() ?? "";
        if (string.IsNullOrEmpty(summary) || summary.Length > 4000 || findings.Length > 16000)
            throw new ClsPublicationException(ClsPublicationError.InvalidInput);
        return uow.ExecuteAsync(async t =>
        {
            var context = await ContextAsync(itemId, roomId, true, t);
            CheckVersion(context.Result, version);
            var now = await clock.GetUtcNowAsync(t);
            var doctors = await identity.GetRoomReadingDoctorsAsync(roomId, now, t);
            var doctor = SelectDoctor(doctors, request.ReadingDoctorId, context.Result.ApprovedByStaffId);
            context.Result.ResultSummary = summary; context.Result.DetailedFindings = findings;
            context.Result.ApprovedByStaffId = doctor.StaffId;
            await store.SaveAsync(context, roomId, actorId, now, false, t);
            return Draft(context, doctors);
        }, ct);
    }

    public Task<ClsPublicationReceipt> PublishAsync(Guid itemId, Guid roomId, Guid actorId, ClsPublishRequest request, string key, CancellationToken ct)
    {
        Ids(itemId, roomId, actorId);
        var version = Version(request.Version);
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(c => c < '!' || c > '~'))
            throw new ClsPublicationException(ClsPublicationError.InvalidInput);
        var caller = "AppUser:" + actorId.ToString("D");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { itemId, roomId, Version = Convert.ToBase64String(version), request.ReadingDoctorId })));
        return uow.ExecuteAsync(async t =>
        {
            switch (await idempotency.BeginAsync(caller, Operation, key, hash, TimeSpan.FromHours(24), null, t))
            {
                case IdempotencyBeginResult.Replay replay: return JsonSerializer.Deserialize<ClsPublicationReceipt>(replay.BodyJson!)!;
                case IdempotencyBeginResult.Conflict: throw new ClsPublicationException(ClsPublicationError.IdempotencyConflict);
                case IdempotencyBeginResult.InProgress: throw new ClsPublicationException(ClsPublicationError.RequestInProgress);
            }
            var context = await ContextAsync(itemId, roomId, true, t);
            CheckVersion(context.Result, version);
            if (string.IsNullOrWhiteSpace(context.Result.ResultSummary))
                throw new ClsPublicationException(ClsPublicationError.InvalidInput);
            var now = await clock.GetUtcNowAsync(t);
            var doctors = await identity.GetRoomReadingDoctorsAsync(roomId, now, t);
            var doctor = SelectDoctor(doctors, request.ReadingDoctorId, context.Result.ApprovedByStaffId);
            // shortcut: this precheck cannot exclude a new upload; replace it with Documents' atomic guard when its owner supplies one.
            var files = await documents.GetAttachmentsByQueueTicketIdAsync(context.QueueTicketId, roomId, t);
            if (files.Any(f => f.UploadStatus != "Uploaded" || f.UploadedAtUtc is null))
                throw new ClsPublicationException(ClsPublicationError.FilesNotReady);
            context.Result.ApprovedByStaffId = doctor.StaffId;
            context.Result.ResultStatus = ResultStatus.Published; context.Result.PublishedAtUtc = now;
            context.Clinical.Item.ItemStatus = ItemStatus.Completed;
            await store.SaveAsync(context, roomId, actorId, now, true, t);
            await documents.MarkClsResultFilesPublishedAsync(context.Result.Id, t);
            await publisher.PublishAsync(new ResultPublished(Guid.NewGuid(), context.Clinical.Encounter.Id, context.Clinical.Order.Id,
                context.Result.Id, itemId, now, null), context.Result.Id, t);
            var receipt = new ClsPublicationReceipt(context.Result.Id, itemId, "Published", Convert.ToBase64String(context.Result.RowVersion), now);
            await idempotency.CompleteAsync(caller, Operation, key, 200, JsonSerializer.Serialize(receipt), null, t);
            return receipt;
        }, ct);
    }

    private async Task<ClsResultContext> ContextAsync(Guid itemId, Guid roomId, bool forUpdate, CancellationToken ct)
    {
        var ticket = await tickets.GetByItemAsync(itemId, ct)
            ?? throw new ClsPublicationException(ClsPublicationError.ResultNotFound);
        if (ticket.RoomId != roomId) throw new ClsPublicationException(ClsPublicationError.Forbidden);
        if (ticket.QueueStatus != "Finished") throw new ClsPublicationException(ClsPublicationError.ServiceNotFinished);
        var context = await store.LoadAsync(itemId, forUpdate, ct)
            ?? throw new ClsPublicationException(ClsPublicationError.ResultNotFound);
        if (context.Clinical.Encounter.Id != ticket.EncounterId) throw new ClsPublicationException(ClsPublicationError.Forbidden);
        if (context.Result.ResultStatus == ResultStatus.Published) throw new ClsPublicationException(ClsPublicationError.ResultAlreadyPublished);
        if (context.Clinical.Encounter.Status is EncounterStatus.Completed or EncounterStatus.Abandoned)
            throw new ClsPublicationException(ClsPublicationError.EncounterClosed);
        if (context.Clinical.Order.OrderBatchStatus != OrderBatchStatus.Confirmed || context.Result.ResultStatus != ResultStatus.Draft)
            throw new ClsPublicationException(ClsPublicationError.OrderNotActive);
        if (context.Clinical.Item.ItemStatus != ItemStatus.ServiceFinished)
            throw new ClsPublicationException(ClsPublicationError.ServiceNotFinished);
        return context with { QueueTicketId = ticket.TicketId };
    }
    private static RoomReadingDoctorDto SelectDoctor(IReadOnlyList<RoomReadingDoctorDto> doctors, Guid? selectedDoctor, Guid? savedStaff)
    {
        if (doctors.Count == 0) throw new ClsPublicationException(ClsPublicationError.ReadingDoctorUnavailable);
        if (selectedDoctor is { } id)
            return doctors.SingleOrDefault(x => x.DoctorId == id) ?? throw new ClsPublicationException(ClsPublicationError.InvalidInput);
        if (savedStaff is { } staff)
            return doctors.SingleOrDefault(x => x.StaffId == staff) ?? throw new ClsPublicationException(ClsPublicationError.ReadingDoctorUnavailable);
        return doctors.Count == 1 ? doctors[0] : throw new ClsPublicationException(ClsPublicationError.InvalidInput);
    }
    private static ClsResultDraft Draft(ClsResultContext context, IReadOnlyList<RoomReadingDoctorDto> doctors)
    {
        var r = context.Result;
        var chosen = doctors.SingleOrDefault(x => x.StaffId == r.ApprovedByStaffId);
        return new(r.Id, r.ServiceOrderItemId, context.QueueTicketId, "Draft", r.ResultSummary, r.DetailedFindings, Convert.ToBase64String(r.RowVersion),
            chosen?.DoctorId, chosen?.FullName, doctors.Select(x => new ReadingDoctorChoice(x.DoctorId, x.FullName)).ToArray());
    }
    private static byte[] Version(string? version)
    {
        try
        {
            var bytes = Convert.FromBase64String(version ?? "");
            if (bytes.Length == 8) return bytes;
        }
        catch (FormatException) { }
        throw new ClsPublicationException(ClsPublicationError.InvalidInput);
    }
    private static void Ids(Guid itemId, Guid roomId, Guid? actorId = null)
    {
        if (itemId == Guid.Empty || roomId == Guid.Empty || actorId == Guid.Empty)
            throw new ClsPublicationException(ClsPublicationError.InvalidInput);
    }
    private static void CheckVersion(CLSResult r, byte[] expected)
    {
        if (!r.RowVersion.SequenceEqual(expected)) throw new ClsPublicationException(ClsPublicationError.ConcurrencyConflict);
    }
}
