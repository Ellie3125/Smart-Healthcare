using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.ToTable("PaymentIntent");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IntentNumber).HasMaxLength(64);
        builder.Property(x => x.TotalExpectedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(64);
        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.PaymentType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.IntentNumber).IsUnique();
        builder.HasIndex(x => x.PayerAppUserId);
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.BookingAttemptId);
        builder.HasIndex(x => x.EncounterId);
    }
}
