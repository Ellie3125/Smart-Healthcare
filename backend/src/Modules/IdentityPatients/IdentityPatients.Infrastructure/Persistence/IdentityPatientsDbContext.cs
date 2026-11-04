using IdentityPatients.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace IdentityPatients.Infrastructure.Persistence;

public class IdentityPatientsDbContext(DbContextOptions<IdentityPatientsDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<DoctorRank> DoctorRanks => Set<DoctorRank>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomAccount> RoomAccounts => Set<RoomAccount>();
    public DbSet<PatientRepresentative> PatientRepresentatives => Set<PatientRepresentative>();
    public DbSet<Staff> Staffs => Set<Staff>();
    public DbSet<RoomDeviceSession> RoomDeviceSessions => Set<RoomDeviceSession>();
    public DbSet<StaffShiftAssignment> StaffShiftAssignments => Set<StaffShiftAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityPatientsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseUtcDateTime();
}
