using IdentityPatients.Application.Administration;
using IdentityPatients.Application.Auth;
using IdentityPatients.Application.Patients;
using IdentityPatients.Application.Verifications;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace IdentityPatients.Tests;

/// <summary>Quy tắc của CLA-FIX-013: Admin (D5), ràng buộc thay 4 unique (Q7), 404 (D8), IUnitOfWork (D7), khóa JWT (D9).</summary>
public sealed class IdentityRulesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CreateStaffRequest StaffRequest(string type) =>
        new(IdentityTestData.Unique("nv"), $"{Guid.NewGuid():N}@test.local", null!, "mat-khau-123", IdentityTestData.Unique("NV"), "Nhân viên mới", type);

    // --- D5: chỉ Admin quản lý nhân sự loại Admin, không vô hiệu hóa Admin hoạt động cuối cùng ---

    [Fact]
    public async Task CreateAdminStaff_ByNonAdmin_Returns403()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var receptionist = host.Context.AddUser(Roles.Receptionist);
        await host.Context.SaveChangesAsync(Ct);

        var result = await host.Get<IStaffAdminService>().CreateStaffAsync(receptionist.Id, StaffRequest("Admin"), Ct);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(StaffAdminService.AdminManagedByAdminCode, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAdminStaff_ByAdmin_CreatesAccountThatLogsInAsAdmin()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var admin = host.Context.AddUser(Roles.Admin);
        await host.Context.SaveChangesAsync(Ct);

        var result = await host.Get<IStaffAdminService>().CreateStaffAsync(admin.Id, StaffRequest("Admin"), Ct);

        Assert.Equal(201, result.StatusCode);
        Assert.Equal("Admin", result.Value!.StaffType);
        Assert.Equal(Roles.Admin, (await AccountRoles.ResolveAsync(host.Context, result.Value.AppUserId, Ct))?.Role);
    }

    [Fact]
    public async Task CreateStaff_Technician_Returns400ListingAdmin()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var admin = host.Context.AddUser(Roles.Admin);
        await host.Context.SaveChangesAsync(Ct);

        var result = await host.Get<IStaffAdminService>().CreateStaffAsync(admin.Id, StaffRequest("Technician"), Ct);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("InvalidInput", result.ErrorCode);
        Assert.Contains("Admin", result.ErrorDetail);
    }

    [Fact]
    public async Task DeactivateAdmin_ByNonAdmin_Returns403()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var nurse = host.Context.AddUser(Roles.Nurse);
        var target = host.Context.AddStaff(host.Context.AddUser(Roles.Patient), StaffType.Admin);
        host.Context.AddUser(Roles.Admin);
        await host.Context.SaveChangesAsync(Ct);

        var result = await host.Get<IStaffAdminService>().ToggleStaffActiveAsync(nurse.Id, target.Id, false, Ct);

        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task DeactivateLastActiveAdmin_Returns409_ButSucceedsWhenAnotherAdminIsActive()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var ctx = host.Context;
        // Trong transaction của test chỉ có các Admin tạo ở đây; Admin đã commit trong DB test (nếu có) phải tắt tạm.
        await ctx.Staffs.Where(s => s.StaffType == StaffType.Admin).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
        var adminUser = ctx.AddUser(Roles.Admin);
        await ctx.SaveChangesAsync(Ct);
        var adminStaff = await ctx.Staffs.SingleAsync(s => s.AppUserId == adminUser.Id, Ct);
        var service = host.Get<IStaffAdminService>();

        var last = await service.ToggleStaffActiveAsync(adminUser.Id, adminStaff.Id, false, Ct);
        Assert.Equal(409, last.StatusCode);
        Assert.Equal(StaffAdminService.LastActiveAdminCode, last.ErrorCode);

        ctx.AddUser(Roles.Admin);
        await ctx.SaveChangesAsync(Ct);
        var ok = await service.UpdateStaffAsync(adminUser.Id, adminStaff.Id, new UpdateStaffRequest("Quản trị cũ", false), Ct);
        Assert.Equal(200, ok.StatusCode);
        Assert.False(ok.Value!.IsActive);
    }

    // --- Q7: ràng buộc thay unique, chạy tuần tự ---

    private static CreateShiftAssignmentRequest Shift(Guid staffId, Guid roomId, DateOnly date, bool leader) =>
        new(staffId, roomId, date, "Morning", date.ToDateTime(new TimeOnly(0, 0), DateTimeKind.Utc), date.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc), leader);

    [Fact]
    public async Task ShiftAssignment_SecondShiftSameDay_Returns409()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var staff = host.Context.AddStaff(host.Context.AddUser(Roles.Patient), StaffType.Nurse);
        var room = host.Context.AddRoom();
        var room2 = host.Context.AddRoom();
        await host.Context.SaveChangesAsync(Ct);
        var service = host.Get<IStaffAdminService>();
        var date = new DateOnly(2099, 2, 1);

        Assert.Equal(201, (await service.CreateShiftAssignmentAsync(Shift(staff.Id, room.Id, date, false), Ct)).StatusCode);
        var second = await service.CreateShiftAssignmentAsync(Shift(staff.Id, room2.Id, date, false), Ct);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(StaffAdminService.ShiftAlreadyAssignedCode, second.ErrorCode);
        Assert.Equal(201, (await service.CreateShiftAssignmentAsync(Shift(staff.Id, room.Id, date.AddDays(1), false), Ct)).StatusCode);
    }

    [Fact]
    public async Task ShiftAssignment_SecondLeaderSameRoomShift_Returns409_NonLeaderAllowed()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var a = host.Context.AddStaff(host.Context.AddUser(Roles.Patient), StaffType.Nurse);
        var b = host.Context.AddStaff(host.Context.AddUser(Roles.Patient), StaffType.Nurse);
        var c = host.Context.AddStaff(host.Context.AddUser(Roles.Patient), StaffType.Nurse);
        var room = host.Context.AddRoom();
        await host.Context.SaveChangesAsync(Ct);
        var service = host.Get<IStaffAdminService>();
        var date = new DateOnly(2099, 2, 2);

        Assert.Equal(201, (await service.CreateShiftAssignmentAsync(Shift(a.Id, room.Id, date, true), Ct)).StatusCode);
        var secondLeader = await service.CreateShiftAssignmentAsync(Shift(b.Id, room.Id, date, true), Ct);
        Assert.Equal(409, secondLeader.StatusCode);
        Assert.Equal(StaffAdminService.ShiftLeaderExistsCode, secondLeader.ErrorCode);
        Assert.Equal(201, (await service.CreateShiftAssignmentAsync(Shift(c.Id, room.Id, date, false), Ct)).StatusCode);
    }

    private static CreatePatientProfileRequest Profile(string relationship) =>
        new("Bệnh nhân", new DateOnly(1990, 1, 1), "Male", null, "0900000000", "Địa chỉ", relationship);

    [Fact]
    public async Task CreateProfile_SecondActiveSelf_Returns409_OtherRelationshipAllowed()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var user = host.Context.AddUser(Roles.Patient);
        await host.Context.SaveChangesAsync(Ct);
        var service = host.Get<IPatientProfileService>();

        Assert.Equal(201, (await service.CreateProfileAsync(user.Id, Profile("Self"), Ct)).StatusCode);
        var second = await service.CreateProfileAsync(user.Id, Profile("Self"), Ct);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(PatientProfileService.SelfProfileExistsCode, second.ErrorCode);
        Assert.Equal(201, (await service.CreateProfileAsync(user.Id, Profile("Child"), Ct)).StatusCode);
    }

    // --- D8: hồ sơ không thuộc tài khoản trả 404 như hồ sơ không có ---

    [Fact]
    public async Task ProfileOfAnotherAccount_ReadAndUpdate_Return404SameAsMissing()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var owner = host.Context.AddUser(Roles.Patient);
        var other = host.Context.AddUser(Roles.Patient);
        await host.Context.SaveChangesAsync(Ct);
        var service = host.Get<IPatientProfileService>();
        var created = await service.CreateProfileAsync(owner.Id, Profile("Self"), Ct);
        var patientId = created.Value!.PatientId;
        var update = new UpdatePatientProfileRequest("Đổi tên", new DateOnly(1990, 1, 1), "Male", null, "0900000000", "Địa chỉ");

        var read = await service.GetProfileByIdAsync(other.Id, patientId, Ct);
        var write = await service.UpdateProfileAsync(other.Id, patientId, update, Ct);
        var missing = await service.GetProfileByIdAsync(owner.Id, Guid.NewGuid(), Ct);

        foreach (var result in new[] { read, write, missing })
        {
            Assert.Equal(404, result.StatusCode);
            Assert.Equal(PatientProfileService.ProfileNotFoundCode, result.ErrorCode);
            Assert.Equal(missing.ErrorDetail, result.ErrorDetail);
        }
    }

    // --- D7: trạng thái xác minh và AuditLog cùng một unit of work ---

    private sealed class RollbackSignal : Exception;

    [Fact]
    public async Task Verify_InsideFailedUnitOfWork_RollsBackStatusAndAuditLogTogether()
    {
        await using var live = LiveIdentity.Create();
        Guid repId;
        await using (var scope = live.Scope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            var user = ctx.AddUser(Roles.Patient);
            var patient = ctx.AddPatient();
            var rep = new PatientRepresentative
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                PatientId = patient.Id,
                Relationship = Relationship.Self,
                VerificationStatus = VerificationStatus.Pending,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.PatientRepresentatives.Add(rep);
            await ctx.SaveChangesAsync(Ct);
            live.Track(user: user.Id, patient: patient.Id);
            repId = rep.Id;
        }

        var staffUserId = Guid.NewGuid();
        await using (var scope = live.Scope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var service = scope.ServiceProvider.GetRequiredService<IVerificationService>();
            await Assert.ThrowsAsync<RollbackSignal>(() => uow.ExecuteAsync(async token =>
            {
                Assert.Equal(200, (await service.VerifyAsync(staffUserId, repId, token)).StatusCode);
                throw new RollbackSignal();
            }, Ct));
        }

        await using (var scope = live.Scope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            Assert.Equal(VerificationStatus.Pending, (await ctx.PatientRepresentatives.AsNoTracking().SingleAsync(r => r.Id == repId, Ct)).VerificationStatus);
            Assert.False(await shared.Set<AuditLog>().AnyAsync(a => a.RecordId == repId, Ct));
        }

        // Không bị bọc thì commit cả hai.
        await using (var scope = live.Scope())
        {
            Assert.Equal(200, (await scope.ServiceProvider.GetRequiredService<IVerificationService>().VerifyAsync(staffUserId, repId, Ct)).StatusCode);
            Assert.True(await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().AnyAsync(a => a.RecordId == repId, Ct));
        }

        await using (var scope = live.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().Where(a => a.RecordId == repId).ExecuteDeleteAsync(Ct);
        }
    }

    // --- Q7: ràng buộc thay unique, chạy đồng thời trên SQL Server ---

    private static async Task<T[]> RaceAsync<T>(LiveIdentity live, int count, Func<IServiceProvider, Task<T>> action)
    {
        var scopes = Enumerable.Range(0, count).Select(_ => live.Scope()).ToArray();
        try
        {
            using var gate = new SemaphoreSlim(0);
            var tasks = scopes.Select(async scope =>
            {
                await gate.WaitAsync(Ct);
                return await action(scope.ServiceProvider);
            }).ToArray();
            gate.Release(count);
            return await Task.WhenAll(tasks);
        }
        finally
        {
            foreach (var scope in scopes)
            {
                await scope.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Concurrent_SelfProfiles_OnlyOneCreated()
    {
        await using var live = LiveIdentity.Create();
        Guid userId;
        await using (var scope = live.Scope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            userId = ctx.AddUser(Roles.Patient).Id;
            await ctx.SaveChangesAsync(Ct);
            live.Track(user: userId);
        }

        var results = await RaceAsync(live, 5, sp => sp.GetRequiredService<IPatientProfileService>().CreateProfileAsync(userId, Profile("Self"), Ct));
        foreach (var created in results.Where(r => r.IsSuccess))
        {
            live.Track(patient: created.Value!.PatientId);
        }

        Assert.Equal(1, results.Count(r => r.StatusCode == 201));
        Assert.Equal(4, results.Count(r => r.ErrorCode == PatientProfileService.SelfProfileExistsCode));
    }

    [Fact]
    public async Task Concurrent_ShiftsSameStaffSameDay_OnlyOneCreated()
    {
        await using var live = LiveIdentity.Create();
        Guid staffId;
        Guid[] roomIds;
        await using (var scope = live.Scope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            var user = ctx.AddUser(Roles.Patient);
            staffId = ctx.AddStaff(user, StaffType.Nurse).Id;
            roomIds = Enumerable.Range(0, 5).Select(_ => ctx.AddRoom().Id).ToArray();
            await ctx.SaveChangesAsync(Ct);
            live.Track(user: user.Id);
            foreach (var room in roomIds) live.Track(room: room);
        }

        var date = new DateOnly(2099, 2, 3);
        var index = -1;
        var results = await RaceAsync(live, 5, sp => sp.GetRequiredService<IStaffAdminService>()
            .CreateShiftAssignmentAsync(Shift(staffId, roomIds[Interlocked.Increment(ref index)], date, false), Ct));

        Assert.Equal(1, results.Count(r => r.StatusCode == 201));
        Assert.Equal(4, results.Count(r => r.ErrorCode == StaffAdminService.ShiftAlreadyAssignedCode));
    }

    [Fact]
    public async Task Concurrent_LeadersSameRoomShift_OnlyOneCreated()
    {
        await using var live = LiveIdentity.Create();
        Guid roomId;
        Guid[] staffIds;
        await using (var scope = live.Scope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            roomId = ctx.AddRoom().Id;
            var users = Enumerable.Range(0, 5).Select(_ => ctx.AddUser(Roles.Patient)).ToArray();
            staffIds = users.Select(u => ctx.AddStaff(u, StaffType.Nurse).Id).ToArray();
            await ctx.SaveChangesAsync(Ct);
            live.Track(room: roomId);
            foreach (var user in users) live.Track(user: user.Id);
        }

        var date = new DateOnly(2099, 2, 4);
        var index = -1;
        var results = await RaceAsync(live, 5, sp => sp.GetRequiredService<IStaffAdminService>()
            .CreateShiftAssignmentAsync(Shift(staffIds[Interlocked.Increment(ref index)], roomId, date, true), Ct));

        Assert.Equal(1, results.Count(r => r.StatusCode == 201));
        Assert.Equal(4, results.Count(r => r.ErrorCode == StaffAdminService.ShiftLeaderExistsCode));
    }

    [Fact]
    public async Task Concurrent_RoomLogins_LeaveExactlyOneActiveSession()
    {
        await using var live = LiveIdentity.Create();
        AppUser user;
        Guid roomAccountId;
        await using (var scope = live.Scope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            user = ctx.AddUser(Roles.RoomAccount);
            await ctx.SaveChangesAsync(Ct);
            var account = await ctx.RoomAccounts.SingleAsync(r => r.AppUserId == user.Id, Ct);
            roomAccountId = account.Id;
            live.Track(user: user.Id, room: account.RoomId);
        }

        var results = await RaceAsync(live, 5, sp => new AuthService(
                sp.GetRequiredService<IdentityPatientsDbContext>(), new NoTokens(),
                sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<AuditLogWriter>())
            .LoginStaffAsync(new LoginRequest(user.UserName, IdentityTestData.Password), Ct));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        await using var check = live.Scope();
        var sessions = await check.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>().RoomDeviceSessions
            .AsNoTracking().Where(s => s.RoomAccountId == roomAccountId).ToListAsync(Ct);
        Assert.Equal(5, sessions.Count);
        Assert.Single(sessions, s => s.IsActive);
    }

    [Fact]
    public async Task Concurrent_DeactivateTwoLastAdmins_KeepsOneActive()
    {
        await using var live = LiveIdentity.Create();
        Guid[] adminUsers;
        Guid[] adminStaff;
        List<Guid> previouslyActive;
        await using (var scope = live.Scope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            // Admin đã commit sẵn trong DB test (nếu có) được tắt tạm rồi bật lại khi xong.
            previouslyActive = await ctx.Staffs.Where(s => s.StaffType == StaffType.Admin && s.IsActive).Select(s => s.Id).ToListAsync(Ct);
            await ctx.Staffs.Where(s => previouslyActive.Contains(s.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
            var users = new[] { ctx.AddUser(Roles.Admin), ctx.AddUser(Roles.Admin) };
            await ctx.SaveChangesAsync(Ct);
            adminUsers = users.Select(u => u.Id).ToArray();
            var staffOf = await ctx.Staffs.Where(s => adminUsers.Contains(s.AppUserId)).ToDictionaryAsync(s => s.AppUserId, s => s.Id, Ct);
            adminStaff = adminUsers.Select(u => staffOf[u]).ToArray();
            foreach (var id in adminUsers) live.Track(user: id);
        }

        try
        {
            var index = -1;
            var results = await RaceAsync(live, 2, sp =>
            {
                var i = Interlocked.Increment(ref index);
                // Mỗi Admin vô hiệu hóa người kia.
                return sp.GetRequiredService<IStaffAdminService>().ToggleStaffActiveAsync(adminUsers[i], adminStaff[1 - i], false, Ct);
            });

            Assert.Equal(1, results.Count(r => r.StatusCode == 200));
            Assert.Equal(1, results.Count(r => r.ErrorCode == StaffAdminService.LastActiveAdminCode));
        }
        finally
        {
            await using var scope = live.Scope();
            await scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>().Staffs
                .Where(s => previouslyActive.Contains(s.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), CancellationToken.None);
        }
    }

    private sealed class NoTokens : ITokenService
    {
        public string GenerateAccessToken(AppUser user, string role, string channel, Guid? doctorId = null, Guid? roomId = null) => "token";
    }

    // --- D9: khóa ký JWT bắt buộc khi khởi động ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcde")] // 31 byte
    public void AddIdentityPatientsAuth_WithoutSigningKeyOf32Bytes_Throws(string? key)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = key }).Build();

        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddIdentityPatientsAuth(configuration));
        Assert.Contains("Jwt:SigningKey", error.Message);
    }

    [Fact]
    public void AddIdentityPatientsAuth_With32ByteKey_Succeeds()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = new string('k', 32) }).Build();

        new ServiceCollection().AddIdentityPatientsAuth(configuration);
    }
}
