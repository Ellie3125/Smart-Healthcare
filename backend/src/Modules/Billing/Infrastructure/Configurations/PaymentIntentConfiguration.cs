using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Billing.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Billing.Infrastructure.Configurations;

internal sealed class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(PaymentIntent), table =>
        {
            table.HasEnumCheck<Currency>(nameof(PaymentIntent.Currency));
            table.HasEnumCheck<PaymentIntentProvider>(nameof(PaymentIntent.Provider));
            table.HasEnumCheck<PaymentType>(nameof(PaymentIntent.PaymentType));
            table.HasEnumCheck<PaymentIntentStatus>(nameof(PaymentIntent.Status));
        });
        builder.Property(e => e.IntentNumber).HasMaxLength(50);
        builder.Property(e => e.TotalExpectedAmount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Provider).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.PaymentType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.IntentNumber).IsUnique();
        builder.HasIndex(e => e.PayerAppUserId); // tham chiếu identity.AppUser, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.PatientId); // tham chiếu identity.Patient, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.BookingAttemptId); // tham chiếu workflow.BookingAttempt, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.EncounterId); // tham chiếu clinical.Encounter, không FK chéo schema (MOD-01)
    }
}
