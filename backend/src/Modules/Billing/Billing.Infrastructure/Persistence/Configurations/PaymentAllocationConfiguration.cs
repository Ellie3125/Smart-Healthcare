using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocation", t =>
        {
            t.HasCheckConstraint("CK_PaymentAllocation_AllocatedAmount_Positive", "[AllocatedAmount] > 0");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AllocatedAmount).HasPrecision(18, 2);

        builder.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BillingItem>().WithMany().HasForeignKey(x => x.BillingItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
