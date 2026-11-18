using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DocumentsReporting.Domain.Entities;

namespace DocumentsReporting.Infrastructure.Persistence.Configurations;

internal sealed class ResultAttachmentConfiguration : IEntityTypeConfiguration<ResultAttachment>
{
    public void Configure(EntityTypeBuilder<ResultAttachment> builder)
    {
        builder.ToTable("ResultAttachment");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.R2StorageKey).HasMaxLength(512);
        builder.Property(x => x.OriginalFileName).HasMaxLength(200);
        builder.Property(x => x.FileContentType).HasMaxLength(200);
        builder.Property(x => x.Sha256Hash).HasMaxLength(128);
        builder.Property(x => x.UploadStatus).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(x => x.R2StorageKey).IsUnique();
        builder.HasIndex(x => new { x.PatientId, x.EncounterId });
        builder.HasIndex(x => x.CLSResultId);
        builder.HasIndex(x => x.QueueTicketId);
        builder.HasIndex(x => x.EncounterId);
    }
}
