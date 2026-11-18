using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence.Configurations;

internal sealed class PatientNotificationConfiguration : IEntityTypeConfiguration<PatientNotification>
{
    public void Configure(EntityTypeBuilder<PatientNotification> builder)
    {
        builder.ToTable("PatientNotification");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.SourceEventId, x.PatientId, x.Kind }).IsUnique();
        builder.HasIndex(x => new { x.PatientId, x.CreatedAtUtc, x.Id }).IsDescending(false, true, true);
    }
}
