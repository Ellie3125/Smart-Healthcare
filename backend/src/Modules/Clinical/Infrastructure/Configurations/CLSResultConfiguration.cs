using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class CLSResultConfiguration : IEntityTypeConfiguration<CLSResult>
{
    public void Configure(EntityTypeBuilder<CLSResult> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(CLSResult), table =>
        {
            table.HasEnumCheck<ResultStatus>(nameof(CLSResult.ResultStatus));
        });
        builder.Property(e => e.ResultSummary).HasMaxLength(2000);
        builder.Property(e => e.DetailedFindings).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ResultStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.ServiceOrderItemId).IsUnique();
        builder.HasIndex(e => e.PerformedByStaffId); // tham chiếu identity.Staff, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.ApprovedByStaffId); // tham chiếu identity.Staff, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.ServiceOrderItem).WithMany().HasForeignKey(e => e.ServiceOrderItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
