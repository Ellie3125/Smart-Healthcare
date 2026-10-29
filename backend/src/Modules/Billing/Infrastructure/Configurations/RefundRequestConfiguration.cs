using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Billing.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Billing.Infrastructure.Configurations;

internal sealed class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(RefundRequest), table =>
        {
            table.HasEnumCheck<RefundReason>(nameof(RefundRequest.RefundReason));
            table.HasEnumCheck<PolicyRuleApplied>(nameof(RefundRequest.PolicyRuleApplied));
            table.HasEnumCheck<RefundRequestStatus>(nameof(RefundRequest.Status));
            table.HasCheckConstraint("CK_RefundRequest_PolicyRule", "([RefundReason] = N'CancelledBefore0500' AND [PolicyRuleApplied] = N'FullRefundPre0500') OR ([RefundReason] <> N'CancelledBefore0500' AND [PolicyRuleApplied] = N'CustomApproval')");
        });
        builder.Property(e => e.RequestedAmount).HasPrecision(18, 2);
        builder.Property(e => e.RefundReason).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.PolicyRuleApplied).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.AppointmentId); // tham chiếu scheduling.Appointment, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.DecisionByStaffId); // tham chiếu identity.Staff, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.BillingItem).WithMany().HasForeignKey(e => e.BillingItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.OriginalPaymentTransaction).WithMany().HasForeignKey(e => e.OriginalPaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
