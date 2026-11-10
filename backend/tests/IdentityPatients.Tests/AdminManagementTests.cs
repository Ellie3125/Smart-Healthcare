using IdentityPatients.Application.Administration;
using IdentityPatients.Application.Rooms;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Services;
using SqlServerTests;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class AdminManagementTests
{
    [Fact]
    public async Task Specialty_SpecialtyCodeCannotBeChangedOnUpdate()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var code = IdentityTestData.Unique("ORIG");
        var specialty = new Specialty
        {
            Id = Guid.NewGuid(),
            SpecialtyCode = code,
            SpecialtyName = "Tên Ban Đầu",
            Description = "Mô tả",
            IsActive = true
        };
        dbContext.Specialties.Add(specialty);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = host.Get<IStaffAdminService>();
        var result = await service.UpdateSpecialtyAsync(specialty.Id, new UpdateSpecialtyRequest("Tên Mới", "Mô tả mới", true), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Tên Mới", result.Value?.Name);
        Assert.Equal(code, result.Value?.Code);

        dbContext.ChangeTracker.Clear();
        var fromDb = await dbContext.Specialties.FindAsync([specialty.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(fromDb);
        Assert.Equal(code, fromDb.SpecialtyCode);
    }

    [Fact]
    public async Task CreateDoctor_WhenStaffIsNotDoctor_Returns400BadRequest()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var staffNurse = new Staff
        {
            Id = Guid.NewGuid(),
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            StaffCode = IdentityTestData.Unique("DD"),
            FullName = "Điều Dưỡng Test",
            StaffType = StaffType.Nurse,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Staffs.Add(staffNurse);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = host.Get<IStaffAdminService>();
        var result = await service.CreateDoctorAsync(new CreateDoctorRequest(
            staffNurse.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Bio"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Doctor", result.ErrorDetail);
    }

    [Fact]
    public async Task CreateRoomAccount_WhenRoomAlreadyHasAccount_Returns409Conflict()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomNumber = IdentityTestData.Unique("R"),
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
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            AccountCode = IdentityTestData.Unique("TK"),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.RoomAccounts.Add(existingAccount);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var roomService = host.Get<IRoomManagementService>();
        var result = await roomService.CreateRoomAccountAsync(
            room.Id,
            new CreateRoomAccountRequest(IdentityTestData.Unique("user_room"), "room@test.local", "P@ss123", IdentityTestData.Unique("TK2")), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("đã có tài khoản", result.ErrorDetail);
    }

    [Fact]
    public async Task CreateShiftAssignment_WhenEndsAtUtcBeforeStartsAtUtc_Returns400BadRequest()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            AppUserId = dbContext.AddUser(Roles.Patient).Id,
            StaffCode = IdentityTestData.Unique("BS"),
            FullName = "Bác Sĩ Test",
            StaffType = StaffType.Doctor,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomNumber = IdentityTestData.Unique("PK"),
            RoomName = "Phòng Test",
            RoomType = RoomType.Clinical,
            DepartmentName = "Khoa Test",
            IsActive = true
        };
        dbContext.Staffs.Add(staff);
        dbContext.Rooms.Add(room);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = host.Get<IStaffAdminService>();
        var now = DateTime.UtcNow;
        var result = await service.CreateShiftAssignmentAsync(new CreateShiftAssignmentRequest(
            staff.Id,
            room.Id,
            DateOnly.FromDateTime(now),
            "Morning",
            StartsAtUtc: now,
            EndsAtUtc: now.AddHours(-1), // invalid: ends before starts
            IsShiftLeader: false), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }
}
