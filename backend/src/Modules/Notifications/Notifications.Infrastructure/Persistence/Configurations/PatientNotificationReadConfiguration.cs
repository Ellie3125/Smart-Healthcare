using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence.Configurations;

internal sealed class PatientNotificationReadConfiguration : IEntityTypeConfiguration<PatientNotificationRead>
{
    public void Configure(EntityTypeBuilder<PatientNotificationRead> builder)
    {
        builder.ToTable("PatientNotificationRead");
        builder.HasKey(x => new { x.NotificationId, x.AppUserId });
        builder.HasOne<PatientNotification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
