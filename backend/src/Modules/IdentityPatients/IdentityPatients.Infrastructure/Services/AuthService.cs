using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPatients.Infrastructure.Services;

public sealed class AuthService(
    IdentityPatientsDbContext dbContext,
    ITokenService tokenService) : IAuthService
{
    private readonly IdentityPatientsDbContext _dbContext = dbContext;
    private readonly ITokenService _tokenService = tokenService;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public async Task<AuthResult> LoginStaffAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName, ct);

        if (user is null || !user.IsActive)
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        if (user.Role == Roles.Patient)
        {
            return AuthResult.Failure(403, "Truy cập bị từ chối", "Tài khoản bệnh nhân không được phép đăng nhập kênh nhân viên.");
        }

        if (!Roles.Staff.Contains(user.Role))
        {
            return AuthResult.Failure(403, "Truy cập bị từ chối", "Tài khoản không có quyền truy cập kênh nhân viên.");
        }

        var token = _tokenService.GenerateAccessToken(user);
        var response = new LoginResponse(token, user.Role, user.UserName, user.Id, user.DoctorId, user.RoomId);

        return AuthResult.Success(response);
    }

    public async Task<AuthResult> LoginPatientAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName, ct);

        if (user is null || !user.IsActive)
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return AuthResult.Failure(401, "Đăng nhập thất bại", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        if (user.Role != Roles.Patient)
        {
            return AuthResult.Failure(403, "Truy cập bị từ chối", "Tài khoản nhân viên không được phép đăng nhập kênh bệnh nhân.");
        }

        var token = _tokenService.GenerateAccessToken(user);
        var response = new LoginResponse(token, user.Role, user.UserName, user.Id, user.DoctorId, user.RoomId);

        return AuthResult.Success(response);
    }
}

