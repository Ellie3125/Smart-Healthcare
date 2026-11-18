using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdentityPatients.Tests;

// Kiểm model theo DB-05 và ERD v2.4: không dùng bảng AspNet* của ASP.NET Core Identity.
public sealed class IdentityModelTests
{
    private static IdentityPatientsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<IdentityPatientsDbContext>()
            .UseSqlServer("Server=model-only;Database=model-only;")
            .Options);

    [Fact]
    public void Model_HasNoAspNetIdentityTables()
    {
        using var context = CreateContext();

        Assert.DoesNotContain(context.Model.GetEntityTypes(), e => e.GetTableName()?.StartsWith("AspNet", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AppUser_MapsToIdentityAppUserWithErdColumns()
    {
        using var context = CreateContext();
        var user = context.Model.FindEntityType(typeof(AppUser));

        Assert.NotNull(user);
        Assert.Equal("AppUser", user.GetTableName());
        Assert.Equal("identity", user.GetSchema());
        // Đúng 9 cột của ERD v2.4 gốc: không có cột vai trò, DoctorId, RoomId, RowVersion hay cột khóa tài khoản (D1).
        Assert.Equal(
            new[] { "CreatedAtUtc", "Email", "EmailConfirmed", "Id", "IsActive", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "UserName" },
            user.GetProperties().Select(p => p.Name).Order().ToArray());
        Assert.Equal(128, user.FindProperty(nameof(AppUser.PasswordHash))!.GetMaxLength());
    }

    [Fact]
    public void Model_HasNoColumnOrUniqueIndexOutsideErd()
    {
        using var context = CreateContext();
        var model = context.Model;

        Assert.Null(model.FindEntityType(typeof(Doctor))!.FindProperty("DefaultRoomId"));
        Assert.Null(model.FindEntityType(typeof(Patient))!.FindProperty("OwnerUserId"));
        // Bốn ràng buộc này do service kiểm trong IUnitOfWork, không phải unique trong DB.
        Assert.DoesNotContain(model.FindEntityType(typeof(PatientRepresentative))!.GetIndexes(), i => i.IsUnique && i.Properties.Count == 1);
        Assert.DoesNotContain(model.FindEntityType(typeof(RoomDeviceSession))!.GetIndexes(), i => i.IsUnique && i.Properties[0].Name == "RoomAccountId");
        Assert.DoesNotContain(model.FindEntityType(typeof(StaffShiftAssignment))!.GetIndexes(), i => i.IsUnique);
    }

    [Fact]
    public void AppUser_HasFilteredUniqueIndexOnEmailAndPhoneNumber()
    {
        using var context = CreateContext();
        var user = context.Model.FindEntityType(typeof(AppUser));
        Assert.NotNull(user);

        foreach (var column in new[] { nameof(AppUser.Email), nameof(AppUser.PhoneNumber) })
        {
            var index = user.GetIndexes().Single(i => i.Properties.Count == 1 && i.Properties[0].Name == column);
            Assert.True(index.IsUnique);
            Assert.Equal($"[{column}] IS NOT NULL", index.GetFilter());
        }
    }

    [Fact]
    public void Staff_RoomAccount_PatientRep_ReferenceAppUserWithAppUserId()
    {
        using var context = CreateContext();

        foreach (var type in new[] { typeof(Staff), typeof(RoomAccount), typeof(PatientRepresentative) })
        {
            var entity = context.Model.FindEntityType(type);
            Assert.NotNull(entity);
            var fk = entity.GetForeignKeys().FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(AppUser));
            Assert.NotNull(fk);
            Assert.Contains(fk.Properties, p => p.Name == "AppUserId");
        }
    }
}
