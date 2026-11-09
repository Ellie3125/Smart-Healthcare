using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class IdentityModelTests
{
    private static IdentityPatientsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityPatientsDbContext>()
            .UseSqlServer("Server=localhost;Database=SmartHealthcare_Test;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new IdentityPatientsDbContext(options);
    }

    [Fact]
    public void Model_MapsAllSevenIdentityTablesToIdentitySchema()
    {
        using var context = CreateContext();
        var model = context.Model;

        var userEntity = model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(userEntity);
        Assert.Equal("AspNetUsers", userEntity.GetTableName());
        Assert.Equal("identity", userEntity.GetSchema());

        var roleEntity = model.FindEntityType(typeof(ApplicationRole));
        Assert.NotNull(roleEntity);
        Assert.Equal("AspNetRoles", roleEntity.GetTableName());
        Assert.Equal("identity", roleEntity.GetSchema());

        // Check user roles
        var userRoleEntity = model.GetEntityTypes().FirstOrDefault(e => e.GetTableName() == "AspNetUserRoles");
        Assert.NotNull(userRoleEntity);
        Assert.Equal("identity", userRoleEntity.GetSchema());

        // Check user claims
        var userClaimsEntity = model.GetEntityTypes().FirstOrDefault(e => e.GetTableName() == "AspNetUserClaims");
        Assert.NotNull(userClaimsEntity);
        Assert.Equal("identity", userClaimsEntity.GetSchema());

        // Check role claims
        var roleClaimsEntity = model.GetEntityTypes().FirstOrDefault(e => e.GetTableName() == "AspNetRoleClaims");
        Assert.NotNull(roleClaimsEntity);
        Assert.Equal("identity", roleClaimsEntity.GetSchema());

        // Check user logins
        var userLoginsEntity = model.GetEntityTypes().FirstOrDefault(e => e.GetTableName() == "AspNetUserLogins");
        Assert.NotNull(userLoginsEntity);
        Assert.Equal("identity", userLoginsEntity.GetSchema());

        // Check user tokens
        var userTokensEntity = model.GetEntityTypes().FirstOrDefault(e => e.GetTableName() == "AspNetUserTokens");
        Assert.NotNull(userTokensEntity);
        Assert.Equal("identity", userTokensEntity.GetSchema());
    }

    [Fact]
    public void Model_DoesNotContainLegacyAppUserEntity()
    {
        using var context = CreateContext();
        var model = context.Model;

        var legacyTable = model.GetEntityTypes().FirstOrDefault(e => e.GetTableName() == "AppUser");
        Assert.Null(legacyTable);
    }

    [Fact]
    public void ApplicationUser_ContainsIsActiveAndCreatedAtUtcProperties()
    {
        using var context = CreateContext();
        var userEntity = context.Model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(userEntity);

        var isActiveProp = userEntity.FindProperty(nameof(ApplicationUser.IsActive));
        Assert.NotNull(isActiveProp);
        Assert.Equal(typeof(bool), isActiveProp.ClrType);

        var createdAtProp = userEntity.FindProperty(nameof(ApplicationUser.CreatedAtUtc));
        Assert.NotNull(createdAtProp);
        Assert.Equal(typeof(DateTime), createdAtProp.ClrType);
    }

    [Fact]
    public void ApplicationUser_HasFilteredUniqueIndexOnPhoneNumber()
    {
        using var context = CreateContext();
        var userEntity = context.Model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(userEntity);

        var phoneIndex = userEntity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(ApplicationUser.PhoneNumber)));

        Assert.NotNull(phoneIndex);
        Assert.True(phoneIndex.IsUnique);
        Assert.Equal("[PhoneNumber] IS NOT NULL", phoneIndex.GetFilter());
    }

    [Fact]
    public void Staff_RoomAccount_PatientRep_ReferenceAspNetUsersWithAppUserId()
    {
        using var context = CreateContext();
        var model = context.Model;

        // Staff
        var staffEntity = model.FindEntityType(typeof(Staff));
        Assert.NotNull(staffEntity);
        var staffFk = staffEntity.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(ApplicationUser));
        Assert.NotNull(staffFk);
        Assert.Contains(staffFk.Properties, p => p.Name == "AppUserId");

        // RoomAccount
        var roomEntity = model.FindEntityType(typeof(RoomAccount));
        Assert.NotNull(roomEntity);
        var roomFk = roomEntity.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(ApplicationUser));
        Assert.NotNull(roomFk);
        Assert.Contains(roomFk.Properties, p => p.Name == "AppUserId");

        // PatientRepresentative
        var repEntity = model.FindEntityType(typeof(PatientRepresentative));
        Assert.NotNull(repEntity);
        var repFk = repEntity.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(ApplicationUser));
        Assert.NotNull(repFk);
        Assert.Contains(repFk.Properties, p => p.Name == "AppUserId");
    }
}
