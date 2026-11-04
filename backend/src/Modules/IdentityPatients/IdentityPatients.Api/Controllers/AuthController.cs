using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts.Constants;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    IdentityPatientsDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    ITokenService tokenService) : ControllerBase
{
    /// <summary>
    /// Đăng nhập dành cho nhân viên (Staff: Receptionist, Nurse, Doctor, RoomAccount, Admin).
    /// Bệnh nhân (Patient) đăng nhập ở đây sẽ bị trả về 403 Forbidden.
    /// Sai tài khoản hoặc mật khẩu trả về 401 với thông báo chung chung.
    /// </summary>
    [HttpPost("staff/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StaffLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName, ct);

        if (user == null || !user.IsActive)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var verifyResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        // Nếu là tài khoản Patient thử đăng nhập cổng Staff -> Trả 403 Forbidden
        if (user.Role == Roles.Patient)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "Tài khoản bệnh nhân không có quyền truy cập kênh nhân viên.");
        }

        var token = tokenService.GenerateAccessToken(user);
        var currentUser = tokenService.GetCurrentUserFromClaims(User);
        var response = new AuthResponse(
            AccessToken: token,
            ExpiresInSeconds: 3600,
            User: new(user.Id, user.UserName, user.Role, user.DoctorId, user.RoomId)
        );

        return Ok(response);
    }

    /// <summary>
    /// Đăng nhập dành riêng cho Bệnh nhân.
    /// Bất kỳ vai trò nào khác Patient đều bị từ chối với 403 Forbidden.
    /// </summary>
    [HttpPost("patient/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PatientLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName, ct);

        if (user == null || !user.IsActive)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var verifyResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        // Ngược lại, chỉ cho phép role Patient
        if (user.Role != Roles.Patient)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "Cổng đăng nhập này chỉ dành cho tài khoản bệnh nhân.");
        }

        var token = tokenService.GenerateAccessToken(user);
        var response = new AuthResponse(
            AccessToken: token,
            ExpiresInSeconds: 3600,
            User: new(user.Id, user.UserName, user.Role, user.DoctorId, user.RoomId)
        );

        return Ok(response);
    }

    /// <summary>
    /// Lấy thông tin người dùng hiện tại từ claim của JWT token.
    /// </summary>
    [HttpGet("/me")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var currentUser = tokenService.GetCurrentUserFromClaims(User);
        return Ok(currentUser);
    }
}

