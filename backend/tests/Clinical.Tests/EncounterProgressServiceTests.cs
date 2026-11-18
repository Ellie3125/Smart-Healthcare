using Clinical.Application.Encounters;
using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Shared.Abstractions;
using Xunit;

namespace Clinical.Tests;

// Kết thúc lượt khám khi bác sĩ khám xong vé cuối (trưởng nhóm chốt 18/11/2026).
public sealed class EncounterProgressServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime Now = new(2026, 11, 18, 3, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(EncounterStatus.WaitingCLS, EncounterStatus.ReturningDoctor, 1)]
    [InlineData(EncounterStatus.ReturningDoctor, EncounterStatus.ReturningDoctor, 0)]
    [InlineData(EncounterStatus.InProgress, EncounterStatus.InProgress, 0)]
    public async Task Start_return_visit_moves_only_waiting_cls(EncounterStatus from, EncounterStatus to, int saves)
    {
        var store = new Store(from, published: false);
        await new EncounterProgressService(store, new Clock()).StartReturnVisitAsync(store.Encounter.Id, Guid.NewGuid(), Ct);
        Assert.Equal((to, saves), (store.Encounter.Status, store.Saves));
    }

    [Theory]
    [InlineData(EncounterStatus.InProgress)]
    [InlineData(EncounterStatus.ReturningDoctor)]
    public async Task Finish_completes_open_encounter_only_after_record_is_published(EncounterStatus from)
    {
        var unpublished = new Store(from, published: false);
        var error = await Assert.ThrowsAsync<ClinicalException>(() =>
            new EncounterProgressService(unpublished, new Clock()).FinishConsultationAsync(unpublished.Encounter.Id, Guid.NewGuid(), Ct));
        Assert.Equal((ClinicalErrorCode.RecordNotPublished, from, 0), (error.Code, unpublished.Encounter.Status, unpublished.Saves));

        var published = new Store(from, published: true);
        await new EncounterProgressService(published, new Clock()).FinishConsultationAsync(published.Encounter.Id, Guid.NewGuid(), Ct);
        Assert.Equal((EncounterStatus.Completed, (DateTime?)Now, 1, from), (published.Encounter.Status, published.Encounter.CompletedAtUtc, published.Saves, published.From));
    }

    [Theory]
    [InlineData(EncounterStatus.WaitingCLS)]
    [InlineData(EncounterStatus.Completed)]
    public async Task Finish_keeps_encounter_waiting_for_cls_or_already_completed(EncounterStatus from)
    {
        var store = new Store(from, published: false);
        await new EncounterProgressService(store, new Clock()).FinishConsultationAsync(store.Encounter.Id, Guid.NewGuid(), Ct);
        Assert.Equal((from, 0), (store.Encounter.Status, store.Saves));
    }

    [Fact]
    public async Task Abandoned_missing_or_empty_ids_are_rejected()
    {
        var abandoned = new Store(EncounterStatus.Abandoned, published: true);
        var service = new EncounterProgressService(abandoned, new Clock());
        Assert.Equal(ClinicalErrorCode.EncounterClosed,
            (await Assert.ThrowsAsync<ClinicalException>(() => service.FinishConsultationAsync(abandoned.Encounter.Id, Guid.NewGuid(), Ct))).Code);
        Assert.Equal(ClinicalErrorCode.EncounterNotFound,
            (await Assert.ThrowsAsync<ClinicalException>(() => service.StartReturnVisitAsync(Guid.NewGuid(), Guid.NewGuid(), Ct))).Code);
        Assert.Equal(ClinicalErrorCode.InvalidRequest,
            (await Assert.ThrowsAsync<ClinicalException>(() => service.FinishConsultationAsync(abandoned.Encounter.Id, Guid.Empty, Ct))).Code);
    }

    private sealed class Clock : IDbClock
    {
        public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(Now);
    }

    private sealed class Store(EncounterStatus status, bool published) : IEncounterProgressStore
    {
        public Encounter Encounter { get; } = new() { Id = Guid.NewGuid(), Status = status };
        public int Saves { get; private set; }
        public EncounterStatus? From { get; private set; }
        public Task<Encounter?> LockAsync(Guid encounterId, CancellationToken ct) => Task.FromResult(encounterId == Encounter.Id ? Encounter : null);
        public Task<bool> IsRecordPublishedAsync(Guid encounterId, CancellationToken ct) => Task.FromResult(published);
        public Task SaveAsync(Encounter encounter, EncounterStatus from, Guid performedByUserId, DateTime nowUtc, CancellationToken ct)
        {
            Saves++; From = from; return Task.CompletedTask;
        }
    }
}
