using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class CLSResultConfiguration : IEntityTypeConfiguration<CLSResult>
{
    public void Configure(EntityTypeBuilder<CLSResult> builder)
    {
        builder.ToTable("CLSResult");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ResultSummary).HasColumnType("nvarchar(max)");
        builder.Property(x => x.DetailedFindings).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ResultStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<ServiceOrderItem>().WithMany().HasForeignKey(x => x.ServiceOrderItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ServiceOrderItemId).IsUnique();
        builder.HasIndex(x => x.PerformedByStaffId);
        builder.HasIndex(x => x.ApprovedByStaffId);
    }
}
