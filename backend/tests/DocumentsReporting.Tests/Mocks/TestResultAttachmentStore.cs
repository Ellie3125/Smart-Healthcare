using DocumentsReporting.Application;
using DocumentsReporting.Domain.Entities;
using DocumentsReporting.Domain.Enums;

namespace DocumentsReporting.Tests.Mocks;

public sealed class TestResultAttachmentStore : IResultAttachmentStore
{
    private readonly List<ResultAttachment> _attachments = [];

    public IReadOnlyList<ResultAttachment> AllAttachments => _attachments.AsReadOnly();

    public Task<ResultAttachment?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_attachments.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<ResultAttachment>> GetByClsResultIdAsync(Guid clsResultId, CancellationToken ct = default)
    {
        IReadOnlyList<ResultAttachment> list = _attachments.Where(x => x.CLSResultId == clsResultId).ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<ResultAttachment>> GetByQueueTicketIdAsync(Guid queueTicketId, CancellationToken ct = default)
    {
        IReadOnlyList<ResultAttachment> list = _attachments.Where(x => x.QueueTicketId == queueTicketId).ToList();
        return Task.FromResult(list);
    }

    public Task<ResultAttachment?> FindLatestDraftByTicketAndNameAsync(Guid queueTicketId, string fileName, CancellationToken ct = default) =>
        Task.FromResult(_attachments
            .Where(x => x.QueueTicketId == queueTicketId && x.OriginalFileName == fileName && x.UploadStatus == UploadStatus.Draft)
            .OrderByDescending(x => x.AttachmentVersion)
            .FirstOrDefault());

    public Task AddAsync(ResultAttachment attachment, CancellationToken ct = default)
    {
        _attachments.Add(attachment);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ResultAttachment attachment, CancellationToken ct = default)
    {
        var idx = _attachments.FindIndex(x => x.Id == attachment.Id);
        if (idx >= 0)
        {
            _attachments[idx] = attachment;
        }
        else
        {
            _attachments.Add(attachment);
        }

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}
