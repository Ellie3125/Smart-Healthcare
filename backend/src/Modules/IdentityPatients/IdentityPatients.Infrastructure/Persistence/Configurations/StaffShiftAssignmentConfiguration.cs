using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Infrastructure.Persistence.Configurations;

internal sealed class StaffShiftAssignmentConfiguration : IEntityTypeConfiguration<StaffShiftAssignment>
{
    public void Configure(EntityTypeBuilder<StaffShiftAssignment> builder)
    {
        builder.ToTable("StaffShiftAssignment");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ShiftName).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<Staff>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}
