using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPatients.Api;
using IdentityPatients.Application.Auth;
using IdentityPatients.Application.Patients;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using SqlServerTests;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class PatientRegistrationAndProfileTests
{
    private const string ValidPhone = "0912345678";

    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateAccessToken(AppUser user, string role, string channel, Guid? doctorId = null, Guid? roomId = null) =>
            "fake-jwt-token";
    }

    [Fact]
    public async Task CreateProfile_FR01ValidationRules_RejectInvalidInputsWith400()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var ct = TestContext.Current.CancellationToken;
        var userId = host.Context.AddUser(Roles.Patient).Id;
        await host.Context.SaveChangesAsync(ct);

        var service = host.Get<IPatientProfileService>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var cases = new[]
        {
            new CreatePatientProfileRequest("Tên hợp lệ", today.AddDays(1), "Male", null, ValidPhone, "Hà Nội", "Self"), // ngày sinh tương lai
            new CreatePatientProfileRequest("Tên hợp lệ", today.AddYears(-121), "Male", null, ValidPhone, "Hà Nội", "Self"), // > 120 tuổi
            new CreatePatientProfileRequest("Tên hợp lệ", new DateOnly(1990, 1, 1), "Male", "12345", ValidPhone, "Hà Nội", "Self"), // CCCD != 12 số
            new CreatePatientProfileRequest("Tên hợp lệ", new DateOnly(1990, 1, 1), "Male", "12345678901A", ValidPhone, "Hà Nội", "Self"), // CCCD chứa chữ
            new CreatePatientProfileRequest("Tên hợp lệ", new DateOnly(1990, 1, 1), "Male", null, "12345", "Hà Nội", "Self"), // SĐT sai định dạng VN
            new CreatePatientProfileRequest("Tên hợp lệ", new DateOnly(1990, 1, 1), "Male", null, ValidPhone, "   ", "Self"), // Địa chỉ trống
            new CreatePatientProfileRequest(new string('A', 201), new DateOnly(1990, 1, 1), "Male", null, ValidPhone, "Hà Nội", "Self"), // Tên > 200
        };

        foreach (var req in cases)
        {
            var res = await service.CreateProfileAsync(userId, req, ct);
            Assert.False(res.IsSuccess);
            Assert.Equal(400, res.StatusCode);
        }
    }

    [Fact]
    public async Task DuplicateNationalId_Returns409GenericMessage_WithoutLeakingOwner()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var ct = TestContext.Current.CancellationToken;

        var duplicateCccd = $"079{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        host.Context.AddPatient(duplicateCccd);
        var patientUserId = host.Context.AddUser(Roles.Patient).Id;
        await host.Context.SaveChangesAsync(ct);

        var service = host.Get<IPatientProfileService>();
        var res = await service.CreateProfileAsync(
            patientUserId,
            new CreatePatientProfileRequest("Trần Văn B", new DateOnly(1995, 5, 5), "Male", duplicateCccd, ValidPhone, "TP.HCM", "Self"),
            ct);

        Assert.False(res.IsSuccess);
        Assert.Equal(409, res.StatusCode);
        Assert.Equal("Số CCCD đã được đăng ký, vui lòng liên hệ quầy tiếp tân", res.ErrorDetail);
        Assert.DoesNotContain(duplicateCccd, res.ErrorDetail ?? string.Empty);
    }

    [Fact]
    public async Task VerifiedProfile_PatientCannotAlterIdentityFields_StaffUpdateRequiresReasonAndMasksAuditCccd()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var ct = TestContext.Current.CancellationToken;
        var dbContext = host.Context;
        var sharedDbContext = host.Get<SharedDbContext>();

        var cccdOld = "079090001111";
        var cccdNew = "079090002222";
        var userId = dbContext.AddUser(Roles.Patient).Id;
        var staffUserId = Guid.NewGuid();
        var patient = dbContext.AddPatient(cccdOld);
        var rep = new PatientRepresentative
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            PatientId = patient.Id,
            Relationship = Relationship.Self,
            VerificationStatus = VerificationStatus.Verified,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.PatientRepresentatives.Add(rep);
        await dbContext.SaveChangesAsync(ct);

        var service = host.Get<IPatientProfileService>();

        // Bệnh nhân cố đổi FullName khi đã Verified -> 403 VerifiedProfileIdentityLocked
        var blocked = await service.UpdateProfileAsync(
            userId,
            patient.Id,
            new UpdatePatientProfileRequest("Tên Bị Đổi", patient.DateOfBirth, patient.Gender.ToString(), cccdOld, ValidPhone, patient.Address),
            ct);
        Assert.False(blocked.IsSuccess);
        Assert.Equal(403, blocked.StatusCode);
        Assert.Equal("VerifiedProfileIdentityLocked", blocked.ErrorCode);

        // Bệnh nhân đổi SĐT và Địa chỉ (giữ nguyên nhân khẩu) -> 200 OK
        var updatedContact = await service.UpdateProfileAsync(
            userId,
            patient.Id,
            new UpdatePatientProfileRequest(patient.FullName, patient.DateOfBirth, patient.Gender.ToString(), cccdOld, "0988777666", "Địa chỉ mới"),
            ct);
        Assert.True(updatedContact.IsSuccess);
        Assert.Equal("0988777666", updatedContact.Value!.PhoneNumber);

        // Tiếp tân sửa không có lý do -> 400
        var missingReason = await service.UpdateProfileByStaffAsync(
            staffUserId,
            patient.Id,
            new StaffUpdatePatientProfileRequest("Tên Chuẩn CCCD", new DateOnly(1991, 2, 3), "Male", cccdNew, "0988777666", "Địa chỉ mới", "  "),
            ct);
        Assert.False(missingReason.IsSuccess);
        Assert.Equal(400, missingReason.StatusCode);

        // Tiếp tân sửa có lý do -> 200 + AuditLog che 8 số đầu CCCD
        var staffUpdated = await service.UpdateProfileByStaffAsync(
            staffUserId,
            patient.Id,
            new StaffUpdatePatientProfileRequest("Tên Chuẩn CCCD", new DateOnly(1991, 2, 3), "Male", cccdNew, "0988777666", "Địa chỉ mới", "Đối chiếu thẻ CCCD mới tại quầy"),
            ct);
        Assert.True(staffUpdated.IsSuccess);
        Assert.Equal("********2222", staffUpdated.Value!.MaskedNationalId);

        var audit = await sharedDbContext.Set<AuditLog>().SingleAsync(a => a.RecordId == patient.Id && a.TableName == "identity.Patient", ct);
        Assert.Equal("Đối chiếu thẻ CCCD mới tại quầy", audit.ChangeReason);
        Assert.DoesNotContain(cccdOld, audit.OldValuesJson ?? string.Empty);
        Assert.DoesNotContain(cccdNew, audit.NewValuesJson ?? string.Empty);
        // SensitiveDataMasker trên AuditLog giữ 3 ký tự cuối; StaffPatientDto giữ 4 ký tự cuối
        Assert.Contains("*********111", audit.OldValuesJson ?? string.Empty);
        Assert.Contains("*********222", audit.NewValuesJson ?? string.Empty);
    }

    [Fact]
    public async Task RegisterPatientAsync_CreatesUserAndPendingSelfProfileAtomically_AndRejectsConflicts()
    {
        await using var host = await IdentityTestHost.OpenAsync();
        var ct = TestContext.Current.CancellationToken;
        var auth = new AuthService(host.Context, new FakeTokenService(), host.Get<IUnitOfWork>(), host.Get<AuditLogWriter>());
        var dbContext = host.Context;

        var u1 = IdentityTestData.Unique("bn_reg");
        var phone1 = $"09{Random.Shared.Next(10_000_000, 99_999_999)}";
        var email1 = $"{u1}@gmail.com";
        var cccd1 = $"079{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        var ok = await auth.RegisterPatientAsync(new RegisterPatientRequest(
            u1,
            email1,
            phone1,
            "Patient@2026",
            CreateSelfProfile: true,
            SelfProfile: new RegisterPatientProfileRequest("Nguyễn Bệnh Nhân", new DateOnly(1992, 4, 5), "Female", cccd1, phone1, "1 Đường A")),
            ct);
        Assert.True(ok.IsSuccess);
        Assert.Equal(Roles.Patient, ok.Response!.Role);

        var createdRep = await dbContext.PatientRepresentatives.SingleAsync(r => r.AppUserId == ok.Response.UserId, ct);
        Assert.Equal(Relationship.Self, createdRep.Relationship);
        Assert.Equal(VerificationStatus.Pending, createdRep.VerificationStatus);
        Assert.True(createdRep.IsActive);

        // Trùng email -> 409
        var dupEmail = await auth.RegisterPatientAsync(new RegisterPatientRequest(
            IdentityTestData.Unique("bn_reg2"), email1, "0911222333", "Patient@2026"), ct);
        Assert.False(dupEmail.IsSuccess);
        Assert.Equal(409, dupEmail.StatusCode);

        // Hồ sơ Self lỗi (trùng CCCD) -> rollback luôn AppUser vừa tạo trong transaction
        var badUser = IdentityTestData.Unique("bn_rollback");
        var dupCccd = await auth.RegisterPatientAsync(new RegisterPatientRequest(
            badUser,
            $"{badUser}@gmail.com",
            "0922333444",
            "Patient@2026",
            CreateSelfProfile: true,
            SelfProfile: new RegisterPatientProfileRequest("Trùng CCCD", new DateOnly(1992, 4, 5), "Male", cccd1, "0922333444", "2 Đường B")),
            ct);
        Assert.False(dupCccd.IsSuccess);
        Assert.Equal(409, dupCccd.StatusCode);
        Assert.False(await dbContext.AppUsers.AnyAsync(u => u.UserName == badUser, ct));
    }

    [Fact]
    public async Task Http_PatientRegister_RateLimiter_Returns429WhenLimitExceeded()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = TestDatabase.RequiredConnectionString();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "IdentityPatients.Tests";
        builder.Services.AddControllers().AddIdentityPatientsApi();
        builder.Services.AddProblemDetails();
        builder.Services.AddPatientRegisterRateLimiter(permitLimit: 2, window: TimeSpan.FromMinutes(1));
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, connectionString)
            .AddSharedInfrastructure(connectionString)
            .AddUnitOfWorkParticipant<IdentityPatientsDbContext>();

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync(ct);

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        HttpResponseMessage? third = null;
        for (var i = 0; i < 3; i++)
        {
            var body = JsonSerializer.Serialize(new
            {
                userName = IdentityTestData.Unique($"rl_{i}"),
                email = $"{IdentityTestData.Unique("rl")}@gmail.com",
                password = "Patient@2026"
            });
            var resp = await client.PostAsync("/auth/patient/register", new StringContent(body, Encoding.UTF8, "application/json"), ct);
            if (i == 2)
            {
                third = resp;
            }
            else
            {
                Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
                resp.Dispose();
            }
        }

        Assert.NotNull(third);
        using (third)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, third!.StatusCode);
            using var json = JsonDocument.Parse(await third.Content.ReadAsStringAsync(ct));
            Assert.Equal("TooManyRequests", json.RootElement.GetProperty("code").GetString());
        }
    }
}
