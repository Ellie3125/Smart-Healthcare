using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Identity.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Identity.Infrastructure.Configurations;

internal sealed class StaffShiftAssignmentConfiguration : IEntityTypeConfiguration<StaffShiftAssignment>
{
    public void Configure(EntityTypeBuilder<StaffShiftAssignment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(StaffShiftAssignment), table =>
        {
            table.HasEnumCheck<ShiftName>(nameof(StaffShiftAssignment.ShiftName));
        });
        builder.Property(e => e.ShiftName).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(e => e.Staff).WithMany().HasForeignKey(e => e.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Room).WithMany().HasForeignKey(e => e.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}
