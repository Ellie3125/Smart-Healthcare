using IdentityPatients.Application.Administration;
using IdentityPatients.Application.Rooms;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class AdminManagementTests
{
    private sealed class FakeUserManager : UserManager<ApplicationUser>
    {
        public bool CreateSucceeds { get; set; } = true;

        public FakeUserManager()
            : base(
                new MockUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                [],
                [],
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                new LoggerFactory().CreateLogger<UserManager<ApplicationUser>>())
        {
        }

        public override Task<IdentityResult> CreateAsync(ApplicationUser user, string password) =>
            Task.FromResult(CreateSucceeds ? IdentityResult.Success : IdentityResult.Failed(new IdentityError { Description = "Create failed" }));

        public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role) =>
            Task.FromResult(IdentityResult.Success);
    }

    private sealed class MockUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
    }

    private static (IdentityPatientsDbContext dbContext, FakeUserManager userManager) CreateContextAndUserManager()
    {
        var options = new DbContextOptionsBuilder<IdentityPatientsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return (new IdentityPatientsDbContext(options), new FakeUserManager());
    }

    [Fact]
    public async Task Specialty_SpecialtyCodeCannotBeChangedOnUpdate()
    {
        var (dbContext, userManager) = CreateContextAndUserManager();
        var specialty = new Specialty
        {
            Id = Guid.NewGuid(),
            SpecialtyCode = "ORIGINAL_CODE",
            SpecialtyName = "Tên Ban Đầu",
            Description = "Mô tả",
            IsActive = true
        };
        dbContext.Specialties.Add(specialty);
        await dbContext.SaveChangesAsync();

        var service = new StaffAdminService(dbContext, userManager);
        var result = await service.UpdateSpecialtyAsync(specialty.Id, new UpdateSpecialtyRequest("Tên Mới", "Mô tả mới", true));

        Assert.True(result.IsSuccess);
        Assert.Equal("Tên Mới", result.Value?.Name);
        Assert.Equal("ORIGINAL_CODE", result.Value?.Code);

        var fromDb = await dbContext.Specialties.FindAsync(specialty.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("ORIGINAL_CODE", fromDb.SpecialtyCode);
    }

    [Fact]
    public async Task CreateDoctor_WhenStaffIsNotDoctor_Returns400BadRequest()
    {
        var (dbContext, userManager) = CreateContextAndUserManager();
        var staffNurse = new Staff
        {
            Id = Guid.NewGuid(),
            AppUserId = Guid.NewGuid(),
            StaffCode = "DD99",
            FullName = "Điều Dưỡng Test",
            StaffType = StaffType.Nurse,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Staffs.Add(staffNurse);
        await dbContext.SaveChangesAsync();

        var service = new StaffAdminService(dbContext, userManager);
        var result = await service.CreateDoctorAsync(new CreateDoctorRequest(
            staffNurse.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Bio"));

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Doctor", result.ErrorDetail);
    }

    [Fact]
    public async Task CreateRoomAccount_WhenRoomAlreadyHasAccount_Returns409Conflict()
    {
        var (dbContext, userManager) = CreateContextAndUserManager();
        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomNumber = "TEST_ROOM",
            RoomName = "Phòng Test",
            RoomType = RoomType.Laboratory,
            DepartmentName = "Khoa Test",
            IsActive = true
        };
        dbContext.Rooms.Add(room);

        var existingAccount = new RoomAccount
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            AppUserId = Guid.NewGuid(),
            AccountCode = "TK_TEST",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.RoomAccounts.Add(existingAccount);
        await dbContext.SaveChangesAsync();

        var roomService = new RoomManagementService(dbContext, userManager);
        var result = await roomService.CreateRoomAccountAsync(
            room.Id,
            new CreateRoomAccountRequest("user_room", "room@test.local", "P@ss123", "TK_TEST_2"));

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("đã có tài khoản", result.ErrorDetail);
    }

    [Fact]
    public async Task CreateShiftAssignment_WhenEndsAtUtcBeforeStartsAtUtc_Returns400BadRequest()
    {
        var (dbContext, userManager) = CreateContextAndUserManager();
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            AppUserId = Guid.NewGuid(),
            StaffCode = "BS99",
            FullName = "Bác Sĩ Test",
            StaffType = StaffType.Doctor,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomNumber = "PK99",
            RoomName = "Phòng Test",
            RoomType = RoomType.Clinical,
            DepartmentName = "Khoa Test",
            IsActive = true
        };
        dbContext.Staffs.Add(staff);
        dbContext.Rooms.Add(room);
        await dbContext.SaveChangesAsync();

        var service = new StaffAdminService(dbContext, userManager);
        var now = DateTime.UtcNow;
        var result = await service.CreateShiftAssignmentAsync(new CreateShiftAssignmentRequest(
            staff.Id,
            room.Id,
            DateOnly.FromDateTime(now),
            "Morning",
            StartsAtUtc: now,
            EndsAtUtc: now.AddHours(-1), // invalid: ends before starts
            IsShiftLeader: false));

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }
}
