using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class ServiceOrderItemConfiguration : IEntityTypeConfiguration<ServiceOrderItem>
{
    public void Configure(EntityTypeBuilder<ServiceOrderItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(ServiceOrderItem), table =>
        {
            table.HasEnumCheck<ItemStatus>(nameof(ServiceOrderItem.ItemStatus));
        });
        builder.Property(e => e.ServiceNameSnapshot).HasMaxLength(200);
        builder.Property(e => e.AppliedPrice).HasPrecision(18, 2);
        builder.Property(e => e.ItemStatus).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.MedicalServiceId); // tham chiếu catalog.MedicalService, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.ServiceOrder).WithMany().HasForeignKey(e => e.ServiceOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
