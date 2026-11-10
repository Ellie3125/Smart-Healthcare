using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class AuthPolicyAndRolesTests
{
    [Fact]
    public void StaffRoles_ContainsAllHospitalStaffRoles()
    {
        Assert.Contains(Roles.Receptionist, Roles.Staff);
        Assert.Contains(Roles.Nurse, Roles.Staff);
        Assert.Contains(Roles.Doctor, Roles.Staff);
        Assert.Contains(Roles.RoomAccount, Roles.Staff);
        Assert.Contains(Roles.Admin, Roles.Staff);
    }

    [Fact]
    public void StaffRoles_DoesNotContainPatient()
    {
        Assert.DoesNotContain(Roles.Patient, Roles.Staff);
    }

    [Fact]
    public void AppClaims_DefinesCorrectClaimNames()
    {
        Assert.Equal("doctor_id", AppClaims.DoctorId);
        Assert.Equal("room_id", AppClaims.RoomId);
    }

    [Fact]
    public void AuthResult_Failure_ReturnsExpectedStatusCodeAndDetails()
    {
        var result = AuthResult.Failure(403, "Truy cập bị từ chối", "Tài khoản bệnh nhân không được phép đăng nhập kênh nhân viên.", "Forbidden");
        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Truy cập bị từ chối", result.ErrorTitle);
        Assert.Contains("bệnh nhân", result.ErrorDetail);
        Assert.Equal("Forbidden", result.ErrorCode);
    }

    [Fact]
    public void AuthResult_Success_ReturnsExpectedLoginResponse()
    {
        var loginResponse = new LoginResponse(
            AccessToken: "sample-token",
            Role: Roles.Receptionist,
            UserName: "letan01",
            UserId: Guid.NewGuid(),
            DoctorId: null,
            RoomId: null);

        var result = AuthResult.Success(loginResponse);
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal("letan01", result.Response.UserName);
        Assert.Equal(Roles.Receptionist, result.Response.Role);
    }
}

