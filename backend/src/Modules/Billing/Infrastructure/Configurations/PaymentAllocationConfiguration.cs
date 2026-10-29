using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Billing.Domain;

namespace SmartHealthcare.Billing.Infrastructure.Configurations;

internal sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(PaymentAllocation), table =>
        {
            table.HasCheckConstraint("CK_PaymentAllocation_AllocatedAmount", "[AllocatedAmount] > 0");
        });
        builder.Property(e => e.AllocatedAmount).HasPrecision(18, 2);
        builder.HasOne(e => e.PaymentTransaction).WithMany().HasForeignKey(e => e.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.BillingItem).WithMany().HasForeignKey(e => e.BillingItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
