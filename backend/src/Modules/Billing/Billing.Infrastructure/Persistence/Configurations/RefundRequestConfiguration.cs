using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        builder.ToTable("RefundRequest", t =>
        {
            t.HasCheckConstraint("CK_RefundRequest_PolicyRule", "([RefundReason] = 'CancelledBefore0500' AND [PolicyRuleApplied] = 'FullRefundPre0500') OR ([RefundReason] <> 'CancelledBefore0500' AND [PolicyRuleApplied] = 'CustomApproval')");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestedAmount).HasPrecision(18, 2);
        builder.Property(x => x.RefundReason).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.PolicyRuleApplied).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<BillingItem>().WithMany().HasForeignKey(x => x.BillingItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.OriginalPaymentTransactionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AppointmentId);
        builder.HasIndex(x => x.DecisionByStaffId);
    }
}
