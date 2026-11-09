using IdentityPatients.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace IdentityPatients.Infrastructure.Persistence;

public sealed class IdentityPatientsDbContext(DbContextOptions<IdentityPatientsDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public const string Schema = "identity";

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Staff> Staffs => Set<Staff>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomAccount> RoomAccounts => Set<RoomAccount>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<DoctorRank> DoctorRanks => Set<DoctorRank>();
    public DbSet<PatientRepresentative> PatientRepresentatives => Set<PatientRepresentative>();
    public DbSet<StaffShiftAssignment> StaffShiftAssignments => Set<StaffShiftAssignment>();
    public DbSet<RoomDeviceSession> RoomDeviceSessions => Set<RoomDeviceSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("AspNetUsers", Schema);
            b.Property(u => u.IsActive).HasDefaultValue(true);
            b.Property(u => u.CreatedAtUtc).HasColumnType("datetime2");
            b.HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");
        });

        modelBuilder.Entity<ApplicationRole>(b => b.ToTable("AspNetRoles", Schema));
        modelBuilder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("AspNetUserRoles", Schema));
        modelBuilder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("AspNetUserClaims", Schema));
        modelBuilder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("AspNetRoleClaims", Schema));
        modelBuilder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("AspNetUserLogins", Schema));
        modelBuilder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("AspNetUserTokens", Schema));

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityPatientsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseUtcDateTime();
}
