using DocumentsReporting.Domain.Entities;

namespace DocumentsReporting.Application;

public interface IResultAttachmentStore
{
    Task<ResultAttachment?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ResultAttachment>> GetByClsResultIdAsync(Guid clsResultId, CancellationToken ct = default);

    Task<IReadOnlyList<ResultAttachment>> GetByQueueTicketIdAsync(Guid queueTicketId, CancellationToken ct = default);

    Task<ResultAttachment?> FindLatestDraftByTicketAndNameAsync(Guid queueTicketId, string fileName, CancellationToken ct = default);

    Task AddAsync(ResultAttachment attachment, CancellationToken ct = default);

    Task UpdateAsync(ResultAttachment attachment, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
