using IdentityPatients.Application.Administration;
using IdentityPatients.Application.Auth;
using IdentityPatients.Application.Rooms;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using SqlServerTests;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class AdminManagementTests
{
    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateAccessToken(AppUser user, string role, string channel, Guid? doctorId = null, Guid? roomId = null) =>
            "fake-jwt-token";
    }

    private static (AppUser User, Staff Staff) AddStaffUser(IdentityPatientsDbContext dbContext, string role, bool isActive = true)
    {
        var user = dbContext.AddUser(role, isActive);
        var staff = dbContext.Staffs.Local.Single(s => s.AppUserId == user.Id);
        return (user, staff);
    }
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

    [Fact]
    public async Task DeactivateSpecialty_WithActiveDoctor_Returns409_SucceedsAfterDoctorDeactivated()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var ct = TestContext.Current.CancellationToken;

        var (_, admin) = AddStaffUser(dbContext, Roles.Admin);
        var (_, doctorStaff) = AddStaffUser(dbContext, Roles.Doctor);
        var spec = new Specialty
        {
            Id = Guid.NewGuid(),
            SpecialtyCode = IdentityTestData.Unique("SPEC"),
            SpecialtyName = "Nội Tổng Quát",
            IsActive = true
        };
        var rank = await dbContext.DoctorRanks.FirstOrDefaultAsync(r => r.RankCode == RankCode.Master, ct)
            ?? new DoctorRank
            {
                Id = Guid.NewGuid(),
                RankCode = RankCode.Master,
                RankName = "Thạc sĩ",
                DisplayOrder = 1,
                IsActive = true
            };
        if (dbContext.Entry(rank).State == EntityState.Detached)
        {
            dbContext.DoctorRanks.Add(rank);
        }
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            StaffId = doctorStaff.Id,
            SpecialtyId = spec.Id,
            DoctorRankId = rank.Id,
            IsActive = true
        };
        dbContext.Specialties.Add(spec);
        dbContext.Doctors.Add(doctor);
        await dbContext.SaveChangesAsync(ct);

        var service = host.Get<IStaffAdminService>();
        var blocked = await service.UpdateSpecialtyAsync(
            spec.Id,
            new UpdateSpecialtyRequest(spec.SpecialtyName, null, false),
            ct);
        Assert.False(blocked.IsSuccess);
        Assert.Equal(409, blocked.StatusCode);
        Assert.Equal("SpecialtyHasActiveDoctors", blocked.ErrorCode);

        var deactivateDoc = await service.ToggleDoctorActiveAsync(doctor.Id, false, ct);
        Assert.True(deactivateDoc.IsSuccess);

        var allowed = await service.UpdateSpecialtyAsync(
            spec.Id,
            new UpdateSpecialtyRequest(spec.SpecialtyName, "Đã ngừng hoạt động", false),
            ct);
        Assert.True(allowed.IsSuccess);
        Assert.False(allowed.Value!.IsActive);
        Assert.NotNull(admin);
    }

    [Fact]
    public async Task ShiftAssignment_RejectsInactiveStaffOrRoom_AndOverlappingUtcWindow()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var ct = TestContext.Current.CancellationToken;

        var (_, inactiveStaff) = AddStaffUser(dbContext, Roles.Doctor, isActive: false);
        var (_, activeStaff) = AddStaffUser(dbContext, Roles.Doctor, isActive: true);
        var activeRoom = dbContext.AddRoom();
        var secondRoom = dbContext.AddRoom();
        var inactiveRoom = dbContext.AddRoom();
        inactiveRoom.IsActive = false;
        await dbContext.SaveChangesAsync(ct);

        var service = host.Get<IStaffAdminService>();
        var baseUtc = new DateTime(2099, 5, 10, 1, 0, 0, DateTimeKind.Utc);
        var date = new DateOnly(2099, 5, 10);

        var resInactiveStaff = await service.CreateShiftAssignmentAsync(
            new CreateShiftAssignmentRequest(inactiveStaff.Id, activeRoom.Id, date, "Morning", baseUtc, baseUtc.AddHours(4), false),
            ct);
        Assert.False(resInactiveStaff.IsSuccess);
        Assert.Equal(400, resInactiveStaff.StatusCode);

        var resInactiveRoom = await service.CreateShiftAssignmentAsync(
            new CreateShiftAssignmentRequest(activeStaff.Id, inactiveRoom.Id, date, "Morning", baseUtc, baseUtc.AddHours(4), false),
            ct);
        Assert.False(resInactiveRoom.IsSuccess);
        Assert.Equal(400, resInactiveRoom.StatusCode);

        var okShift = await service.CreateShiftAssignmentAsync(
            new CreateShiftAssignmentRequest(activeStaff.Id, activeRoom.Id, date, "Morning", baseUtc, baseUtc.AddHours(4), true),
            ct);
        Assert.True(okShift.IsSuccess);

        // Trùng khoảng UTC ở ngày kế tiếp / ca khác vẫn bị chặn nếu thời gian giao nhau
        var overlapShift = await service.CreateShiftAssignmentAsync(
            new CreateShiftAssignmentRequest(activeStaff.Id, secondRoom.Id, date.AddDays(1), "Afternoon", baseUtc.AddHours(2), baseUtc.AddHours(6), false),
            ct);
        Assert.False(overlapShift.IsSuccess);
        Assert.Equal(409, overlapShift.StatusCode);
    }

    [Fact]
    public async Task ResetStaffPassword_ByAdmin_AllowsLoginAndAudits_NonAdminBlockedForAdminTarget()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var sharedDbContext = host.Get<SharedDbContext>();
        var ct = TestContext.Current.CancellationToken;

        var adminActor = dbContext.AddUser(Roles.Admin);
        var nonAdminActor = dbContext.AddUser(Roles.Receptionist);
        var (_, targetAdmin) = AddStaffUser(dbContext, Roles.Admin);
        var (targetDoctorUser, targetDoctor) = AddStaffUser(dbContext, Roles.Doctor);
        await dbContext.SaveChangesAsync(ct);

        var service = host.Get<IStaffAdminService>();

        var forbidden = await service.ResetStaffPasswordAsync(
            nonAdminActor.Id,
            targetAdmin.Id,
            new ResetStaffPasswordRequest("NewPass@123"),
            ct);
        Assert.False(forbidden.IsSuccess);
        Assert.Equal(403, forbidden.StatusCode);

        var ok = await service.ResetStaffPasswordAsync(
            adminActor.Id,
            targetDoctor.Id,
            new ResetStaffPasswordRequest("DoctorNew@2026"),
            ct);
        Assert.True(ok.IsSuccess);

        var auth = new AuthService(dbContext, new FakeTokenService(), host.Get<IUnitOfWork>(), host.Get<AuditLogWriter>());
        var loginRes = await auth.LoginStaffAsync(new LoginRequest(targetDoctorUser.UserName, "DoctorNew@2026"), ct);
        Assert.True(loginRes.IsSuccess);

        Assert.True(await sharedDbContext.Set<AuditLog>().AnyAsync(a => a.RecordId == targetDoctorUser.Id && a.TableName == "identity.AppUser" && a.ChangedColumns == "PasswordHash", ct));
    }

    [Fact]
    public async Task ContractModule_Lookups_ExcludeInactiveEntities()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var dbContext = host.Context;
        var ct = TestContext.Current.CancellationToken;

        var (_, activeDocStaff) = AddStaffUser(dbContext, Roles.Doctor);
        var (_, inactiveDocStaff) = AddStaffUser(dbContext, Roles.Doctor);

        var activeSpec = new Specialty
        {
            Id = Guid.NewGuid(),
            SpecialtyCode = IdentityTestData.Unique("SP_ON"),
            SpecialtyName = "Tim Mạch",
            IsActive = true
        };
        var inactiveSpec = new Specialty
        {
            Id = Guid.NewGuid(),
            SpecialtyCode = IdentityTestData.Unique("SP_OFF"),
            SpecialtyName = "Ngưng",
            IsActive = false
        };
        var activeRank = await dbContext.DoctorRanks.FirstOrDefaultAsync(r => r.RankCode == RankCode.SpecialistII, ct)
            ?? new DoctorRank
            {
                Id = Guid.NewGuid(),
                RankCode = RankCode.SpecialistII,
                RankName = "BS CKII",
                DisplayOrder = 1,
                IsActive = true
            };
        if (dbContext.Entry(activeRank).State == EntityState.Detached)
        {
            dbContext.DoctorRanks.Add(activeRank);
        }
        var activeRoom = dbContext.AddRoom();
        var inactiveRoom = dbContext.AddRoom();
        inactiveRoom.IsActive = false;

        var activeDoctor = new Doctor
        {
            Id = Guid.NewGuid(),
            StaffId = activeDocStaff.Id,
            SpecialtyId = activeSpec.Id,
            DoctorRankId = activeRank.Id,
            IsActive = true
        };
        var inactiveDoctor = new Doctor
        {
            Id = Guid.NewGuid(),
            StaffId = inactiveDocStaff.Id,
            SpecialtyId = activeSpec.Id,
            DoctorRankId = activeRank.Id,
            IsActive = false
        };

        var shiftDate = new DateOnly(2099, 6, 1);
        var shift = new StaffShiftAssignment
        {
            Id = Guid.NewGuid(),
            StaffId = activeDocStaff.Id,
            RoomId = inactiveRoom.Id,
            ShiftDate = shiftDate,
            ShiftName = ShiftName.Morning,
            StartsAtUtc = new DateTime(2099, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            EndsAtUtc = new DateTime(2099, 6, 1, 4, 0, 0, DateTimeKind.Utc),
            IsShiftLeader = true
        };

        dbContext.Specialties.AddRange(activeSpec, inactiveSpec);
        dbContext.Doctors.AddRange(activeDoctor, inactiveDoctor);
        dbContext.StaffShiftAssignments.Add(shift);
        await dbContext.SaveChangesAsync(ct);

        var module = host.Get<IIdentityPatientsModule>();
        Assert.NotNull(await module.GetDoctorAsync(activeDoctor.Id, ct));
        Assert.Null(await module.GetDoctorAsync(inactiveDoctor.Id, ct));

        var searched = await module.SearchDoctorsAsync(new DoctorSearch(SpecialtyId: activeSpec.Id), ct);
        Assert.Contains(searched, d => d.DoctorId == activeDoctor.Id);
        Assert.DoesNotContain(searched, d => d.DoctorId == inactiveDoctor.Id);

        Assert.NotNull(await module.GetRoomAsync(activeRoom.Id, ct));
        Assert.Null(await module.GetRoomAsync(inactiveRoom.Id, ct));

        var rooms = await module.GetRoomsAsync(ct: ct);
        Assert.Contains(rooms, r => r.Id == activeRoom.Id);
        Assert.DoesNotContain(rooms, r => r.Id == inactiveRoom.Id);

        // Shift leader ở phòng đang tắt không được trả về
        Assert.Null(await module.GetRoomShiftLeaderAsync(inactiveRoom.Id, shiftDate, "Morning", ct));
    }
}
