using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class CLSResultRevisionConfiguration : IEntityTypeConfiguration<CLSResultRevision>
{
    public void Configure(EntityTypeBuilder<CLSResultRevision> builder)
    {
        builder.ToTable("CLSResultRevision", t =>
        {
            t.HasCheckConstraint("CK_CLSResultRevision_RevisionNumber_Positive", "[RevisionNumber] >= 1");
            t.HasCheckConstraint("CK_CLSResultRevision_Kind",
                "([Kind] = N'Published' AND [RevisionNumber] = 1) OR ([Kind] IN (N'Corrected', N'Withdrawn') AND [RevisionNumber] > 1 AND [Reason] IS NOT NULL AND LEN(LTRIM([Reason])) > 0)");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ResultSummary).HasColumnType("nvarchar(max)");
        builder.Property(x => x.DetailedFindings).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Reason).HasMaxLength(CLSResultRevision.ReasonMaxLength);

        builder.HasOne<CLSResult>().WithMany().HasForeignKey(x => x.CLSResultId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CLSResultId, x.RevisionNumber }).IsUnique();
    }
}
