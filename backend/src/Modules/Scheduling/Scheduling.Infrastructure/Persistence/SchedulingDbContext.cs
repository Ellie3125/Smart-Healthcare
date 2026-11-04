using Microsoft.EntityFrameworkCore;
using Scheduling.Domain.Entities;
using Shared.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Persistence;

public sealed class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : DbContext(options)
{
    public const string Schema = "scheduling";

    public DbSet<DoctorDayQuota> DoctorDayQuotas => Set<DoctorDayQuota>();
    public DbSet<ActiveBookingClaim> ActiveBookingClaims => Set<ActiveBookingClaim>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<BookingAttempt> BookingAttempts => Set<BookingAttempt>();
    public DbSet<ServiceStation> ServiceStations => Set<ServiceStation>();
    public DbSet<QueueTicket> QueueTickets => Set<QueueTicket>();
    public DbSet<ActivePatientEngagement> ActivePatientEngagements => Set<ActivePatientEngagement>();
    public DbSet<ActiveStationAllocation> ActiveStationAllocations => Set<ActiveStationAllocation>();
    public DbSet<MedicalService> MedicalServices => Set<MedicalService>();
    public DbSet<MedicalServiceRoom> MedicalServiceRooms => Set<MedicalServiceRoom>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulingDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseUtcDateTime();
}
