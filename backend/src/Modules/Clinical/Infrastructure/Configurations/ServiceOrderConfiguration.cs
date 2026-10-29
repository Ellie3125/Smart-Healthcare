using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class ServiceOrderConfiguration : IEntityTypeConfiguration<ServiceOrder>
{
    public void Configure(EntityTypeBuilder<ServiceOrder> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(ServiceOrder), table =>
        {
            table.HasEnumCheck<OrderBatchStatus>(nameof(ServiceOrder.OrderBatchStatus));
        });
        builder.Property(e => e.OrderBatchStatus).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.PrescribedByDoctorId); // tham chiếu identity.Doctor, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.Encounter).WithMany().HasForeignKey(e => e.EncounterId).OnDelete(DeleteBehavior.Restrict);
    }
}
