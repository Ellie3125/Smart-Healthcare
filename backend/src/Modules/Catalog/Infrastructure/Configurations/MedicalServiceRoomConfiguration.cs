using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Catalog.Domain;

namespace SmartHealthcare.Catalog.Infrastructure.Configurations;

internal sealed class MedicalServiceRoomConfiguration : IEntityTypeConfiguration<MedicalServiceRoom>
{
    public void Configure(EntityTypeBuilder<MedicalServiceRoom> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.RoomId); // tham chiếu identity.Room, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.MedicalService).WithMany().HasForeignKey(e => e.MedicalServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
