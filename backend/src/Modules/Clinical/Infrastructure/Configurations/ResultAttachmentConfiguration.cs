using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class ResultAttachmentConfiguration : IEntityTypeConfiguration<ResultAttachment>
{
    public void Configure(EntityTypeBuilder<ResultAttachment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.R2StorageKey).HasMaxLength(1024);
        builder.Property(e => e.OriginalFileName).HasMaxLength(255);
        builder.Property(e => e.FileContentType).HasMaxLength(127);
        builder.HasIndex(e => e.R2StorageKey).IsUnique();
        builder.HasOne(e => e.CLSResult).WithMany().HasForeignKey(e => e.CLSResultId).OnDelete(DeleteBehavior.Restrict);
    }
}
