using DocumentsReporting.Application;
using DocumentsReporting.Contracts;
using DocumentsReporting.Domain.Entities;
using DocumentsReporting.Domain.Enums;
using DocumentsReporting.Infrastructure.Storage;
using DocumentsReporting.Tests.Mocks;
using Xunit;

namespace DocumentsReporting.Tests;

public sealed class UnitOfWorkPublishTests
{
    [Fact]
    public async Task Publish_InUnitOfWork_WhenRollback_ChangesAreDiscarded()
    {
        var store = new TransactionalFakeStore();
        var clsResultId = Guid.NewGuid();

        var attachment = ResultAttachment.CreateDraft(
            Guid.NewGuid(),
            clsResultId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "results/key1.pdf",
            "kq.pdf",
            "application/pdf",
            1024,
            "hash123",
            1);
        attachment.MarkUploaded(DateTime.UtcNow);
        store.Seed(attachment);

        var storage = new InMemoryObjectStorage();
        var scheduling = new TestSchedulingModule();
        var clinical = new TestClinicalModule();
        var identity = new TestIdentityPatientsModule();
        var audit = new TestAuditLogPort();
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);

        var service = new DocumentsService(store, storage, scheduling, clinical, identity, audit, clock);

        // Giả lập bắt đầu Unit of Work
        store.BeginTransaction();
        try
        {
            // Clinical gọi Documents module đánh dấu Published trong cùng UoW
            await service.MarkClsResultFilesPublishedAsync(clsResultId);

            // Giả lập lỗi ở bước Clinical phát hành
            throw new InvalidOperationException("Clinical publication failed, rolling back UoW");
        }
        catch (InvalidOperationException)
        {
            // UoW Rollback
            store.RollbackTransaction();
        }

        // Sau khi rollback, tệp vẫn chưa IsPublished
        var reloaded = await store.FindByIdAsync(attachment.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsPublished);
    }

    [Fact]
    public async Task Publish_InUnitOfWork_WhenCommitted_ChangesArePersisted()
    {
        var store = new TransactionalFakeStore();
        var clsResultId = Guid.NewGuid();

        var attachment = ResultAttachment.CreateDraft(
            Guid.NewGuid(),
            clsResultId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "results/key1.pdf",
            "kq.pdf",
            "application/pdf",
            1024,
            "hash123",
            1);
        attachment.MarkUploaded(DateTime.UtcNow);
        store.Seed(attachment);

        var storage = new InMemoryObjectStorage();
        var scheduling = new TestSchedulingModule();
        var clinical = new TestClinicalModule();
        var identity = new TestIdentityPatientsModule();
        var audit = new TestAuditLogPort();
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);

        var service = new DocumentsService(store, storage, scheduling, clinical, identity, audit, clock);

        store.BeginTransaction();
        await service.MarkClsResultFilesPublishedAsync(clsResultId);
        store.CommitTransaction();

        var reloaded = await store.FindByIdAsync(attachment.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsPublished);
    }

    private sealed class TransactionalFakeStore : IResultAttachmentStore
    {
        private readonly Dictionary<Guid, ResultAttachment> _committed = new();
        private readonly Dictionary<Guid, ResultAttachment> _active = new();

        public void Seed(ResultAttachment attachment)
        {
            _committed[attachment.Id] = attachment;
            _active[attachment.Id] = Clone(attachment);
        }

        public void BeginTransaction()
        {
            _active.Clear();
            foreach (var kvp in _committed)
            {
                _active[kvp.Key] = Clone(kvp.Value);
            }
        }

        public void CommitTransaction()
        {
            _committed.Clear();
            foreach (var kvp in _active)
            {
                _committed[kvp.Key] = Clone(kvp.Value);
            }
        }

        public void RollbackTransaction()
        {
            _active.Clear();
            foreach (var kvp in _committed)
            {
                _active[kvp.Key] = Clone(kvp.Value);
            }
        }

        public Task<ResultAttachment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        {
            _active.TryGetValue(id, out var att);
            return Task.FromResult(att);
        }

        public Task<IReadOnlyList<ResultAttachment>> GetByClsResultIdAsync(Guid clsResultId, CancellationToken ct = default)
        {
            IReadOnlyList<ResultAttachment> list = _active.Values
                .Where(x => x.CLSResultId == clsResultId)
                .OrderBy(x => x.AttachmentVersion)
                .ToList();
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<ResultAttachment>> GetByQueueTicketIdAsync(Guid queueTicketId, CancellationToken ct = default)
        {
            IReadOnlyList<ResultAttachment> list = _active.Values
                .Where(x => x.QueueTicketId == queueTicketId)
                .OrderBy(x => x.AttachmentVersion)
                .ToList();
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<ResultAttachment>> GetByEncounterIdAsync(Guid encounterId, CancellationToken ct = default)
        {
            IReadOnlyList<ResultAttachment> list = _active.Values
                .Where(x => x.EncounterId == encounterId && x.IsPublished)
                .OrderBy(x => x.AttachmentVersion)
                .ToList();
            return Task.FromResult(list);
        }

        public Task<ResultAttachment?> FindLatestDraftByTicketAndNameAsync(Guid queueTicketId, string fileName, CancellationToken ct = default)
        {
            var att = _active.Values
                .Where(x => x.QueueTicketId == queueTicketId && x.OriginalFileName == fileName && x.UploadStatus == UploadStatus.Draft)
                .OrderByDescending(x => x.AttachmentVersion)
                .FirstOrDefault();
            return Task.FromResult(att);
        }

        public Task AddAsync(ResultAttachment attachment, CancellationToken ct = default)
        {
            _active[attachment.Id] = attachment;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ResultAttachment attachment, CancellationToken ct = default)
        {
            _active[attachment.Id] = attachment;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

        private static ResultAttachment Clone(ResultAttachment source)
        {
            var clone = ResultAttachment.CreateDraft(
                source.Id,
                source.CLSResultId,
                source.QueueTicketId,
                source.EncounterId,
                source.PatientId,
                source.R2StorageKey,
                source.OriginalFileName,
                source.FileContentType,
                source.FileSizeBytes,
                source.Sha256Hash,
                source.AttachmentVersion);

            if (source.UploadStatus == UploadStatus.Uploaded)
            {
                clone.MarkUploaded(source.UploadedAtUtc ?? DateTime.UtcNow);
            }
            else if (source.UploadStatus == UploadStatus.Removed)
            {
                clone.MarkRemoved();
            }

            if (source.IsPublished)
            {
                clone.MarkPublished();
            }

            return clone;
        }
    }
}
