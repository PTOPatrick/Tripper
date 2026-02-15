using Microsoft.EntityFrameworkCore;
using Tripper.Application.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Application.Interfaces.Services;
using Tripper.Core.Entities;
using Tripper.Core.Interfaces;

namespace Tripper.Application.Services;

public sealed class AuthService(IUserRepository users, IPasswordHasher hasher, IJwtTokenService tokenService) : IAuthService
{
    public async Task<Result<AuthResponse>> SignupAsync(SignupRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var username = request.Username.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username))
            return Result<AuthResponse>.Fail(new Error(ErrorType.Validation, "auth.invalid_input", "Email and Username are required."));

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return Result<AuthResponse>.Fail(new Error(ErrorType.Validation, "auth.password_too_short", "Password must be at least 8 characters."));

        if (await users.EmailExistsAsync(email, ct))
            return Result<AuthResponse>.Fail(new Error(ErrorType.Conflict, "auth.email_exists", "Email already exists."));

        if (await users.UsernameExistsAsync(username, ct))
            return Result<AuthResponse>.Fail(new Error(ErrorType.Conflict, "auth.username_exists", "Username already exists."));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = hasher.HashPassword(request.Password)
        };

        await users.AddAsync(user, ct);

        try
        {
            await users.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result<AuthResponse>.Fail(new Error(ErrorType.Conflict, "auth.conflict", "User already exists."));
        }

        var token = tokenService.GenerateToken(user);
        return Result<AuthResponse>.Ok(new AuthResponse(token, user.Id, user.Username));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
            return Result<AuthResponse>.Fail(new Error(ErrorType.Validation, "auth.invalid_input", "Email and Password are required."));

        var user = await users.FindByEmailAsync(email, ct);

        if (user is null || !hasher.VerifyPassword(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Fail(new Error(ErrorType.Unauthorized, "auth.invalid_credentials", "Invalid credentials."));

        var token = tokenService.GenerateToken(user);
        return Result<AuthResponse>.Ok(new AuthResponse(token, user.Id, user.Username));
    }
}