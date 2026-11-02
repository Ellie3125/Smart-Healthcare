using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class MedicalServiceRoomConfiguration : IEntityTypeConfiguration<MedicalServiceRoom>
{
    public void Configure(EntityTypeBuilder<MedicalServiceRoom> builder)
    {
        builder.ToTable("MedicalServiceRoom");
        builder.HasKey(x => x.Id);

        builder.HasOne<MedicalService>().WithMany().HasForeignKey(x => x.MedicalServiceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RoomId);
    }
}
