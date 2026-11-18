using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using SqlServerTests;

namespace IdentityPatients.Tests;

/// <summary>
/// DI thật của IdentityPatients + Shared (IUnitOfWork, AuditLogWriter), nhưng hai DbContext gắn vào transaction của
/// <see cref="TestDatabase"/>: UoW thấy transaction đang mở nên tham gia, mọi thứ rollback khi dispose (DB-06, DB-08).
/// </summary>
internal sealed class IdentityTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _root;
    private readonly AsyncServiceScope _scope;

    private IdentityTestHost(TestDatabase db, ServiceProvider root)
    {
        Db = db;
        _root = root;
        _scope = root.CreateAsyncScope();
    }

    public TestDatabase Db { get; }
    public IdentityPatientsDbContext Context => Get<IdentityPatientsDbContext>();
    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public static async Task<IdentityTestHost> OpenAsync()
    {
        var connectionString = TestDatabase.RequiredConnectionString();
        var db = await TestDatabase.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedInfrastructure(connectionString)
            .AddIdentityPatientsInfrastructure(connectionString)
            .AddUnitOfWorkParticipant<IdentityPatientsDbContext>();
        services.RemoveAll<SharedDbContext>();
        services.RemoveAll<IdentityPatientsDbContext>();
        services.AddScoped(_ => db.CreateContext<SharedDbContext>(options => new(options)));
        services.AddScoped(_ => db.CreateContext<IdentityPatientsDbContext>(options => new(options)));
        return new IdentityTestHost(db, services.BuildServiceProvider());
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
        await Db.DisposeAsync();
    }
}

internal static class IdentityTestData
{
    public const string Password = "test-password-123";

    // Hash một lần cho cả lớp test: PBKDF2 600.000 vòng tốn vài trăm ms mỗi lần.
    private static readonly Lazy<string> PasswordHash = new(() => PasswordHashing.Hash(Password));

    public static IdentityPatientsDbContext Context(this TestDatabase db) =>
        db.CreateContext<IdentityPatientsDbContext>(options => new(options));

    public static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 64)];

    /// <summary>
    /// Tạo AppUser cùng dòng quyết định vai trò (D3): Staff với StaffType, hoặc Room + RoomAccount; Patient chỉ có AppUser.
    /// <paramref name="role"/> là tên vai trò, hoặc "Technician".
    /// </summary>
    public static AppUser AddUser(this IdentityPatientsDbContext context, string role, bool isActive = true, Guid? id = null)
    {
        var user = new AppUser
        {
            Id = id ?? Guid.NewGuid(),
            UserName = Unique("u"),
            PasswordHash = PasswordHash.Value,
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.AppUsers.Add(user);

        if (role == Roles.RoomAccount)
        {
            var room = context.AddRoom();
            context.RoomAccounts.Add(new RoomAccount
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                RoomId = room.Id,
                AccountCode = Unique("TK"),
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else if (role != Roles.Patient)
        {
            context.AddStaff(user, Enum.Parse<StaffType>(role), isActive);
        }

        return user;
    }

    public static Staff AddStaff(this IdentityPatientsDbContext context, AppUser user, StaffType type, bool isActive = true)
    {
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            AppUserId = user.Id,
            StaffCode = Unique("NV"),
            FullName = "Nhân viên test",
            StaffType = type,
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Staffs.Add(staff);
        return staff;
    }

    public static Room AddRoom(this IdentityPatientsDbContext context)
    {
        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomNumber = Unique("R"),
            RoomName = "Phòng test",
            RoomType = RoomType.Laboratory,
            DepartmentName = "Khoa test",
            IsActive = true
        };
        context.Rooms.Add(room);
        return room;
    }

    public static Patient AddPatient(this IdentityPatientsDbContext context, string? nationalId = null)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = "Bệnh nhân test",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = Gender.Male,
            NationalId = nationalId,
            PhoneNumber = "0900000000",
            Address = "Địa chỉ test",
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Patients.Add(patient);
        return patient;
    }
}
