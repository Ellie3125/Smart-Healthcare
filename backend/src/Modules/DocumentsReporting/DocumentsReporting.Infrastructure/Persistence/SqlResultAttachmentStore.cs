using DocumentsReporting.Application;
using DocumentsReporting.Domain.Entities;
using DocumentsReporting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocumentsReporting.Infrastructure.Persistence;

public sealed class SqlResultAttachmentStore(DocumentsDbContext context) : IResultAttachmentStore
{
    public Task<ResultAttachment?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        context.ResultAttachments.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<ResultAttachment>> GetByClsResultIdAsync(Guid clsResultId, CancellationToken ct = default) =>
        await context.ResultAttachments
            .Where(x => x.CLSResultId == clsResultId)
            .OrderBy(x => x.AttachmentVersion)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ResultAttachment>> GetByQueueTicketIdAsync(Guid queueTicketId, CancellationToken ct = default) =>
        await context.ResultAttachments
            .Where(x => x.QueueTicketId == queueTicketId)
            .OrderBy(x => x.AttachmentVersion)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ResultAttachment>> GetByEncounterIdAsync(Guid encounterId, CancellationToken ct = default) =>
        await context.ResultAttachments
            .Where(x => x.EncounterId == encounterId && x.IsPublished)
            .OrderBy(x => x.AttachmentVersion)
            .ToListAsync(ct);

    public Task<ResultAttachment?> FindLatestDraftByTicketAndNameAsync(Guid queueTicketId, string fileName, CancellationToken ct = default) =>
        context.ResultAttachments
            .Where(x => x.QueueTicketId == queueTicketId && x.OriginalFileName == fileName && x.UploadStatus == UploadStatus.Draft)
            .OrderByDescending(x => x.AttachmentVersion)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(ResultAttachment attachment, CancellationToken ct = default) =>
        await context.ResultAttachments.AddAsync(attachment, ct);

    public Task UpdateAsync(ResultAttachment attachment, CancellationToken ct = default)
    {
        context.ResultAttachments.Update(attachment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
