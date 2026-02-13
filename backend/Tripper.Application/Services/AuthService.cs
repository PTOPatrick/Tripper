using Microsoft.EntityFrameworkCore;
using Tripper.Application.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Core.Entities;
using Tripper.Core.Interfaces;
using Tripper.Infra.Auth;
using Tripper.Infra.Data;

namespace Tripper.Application.Services;

public sealed class AuthService(TripperDbContext db, IPasswordHasher hasher, JwtTokenService tokenService) : IAuthService
{
    public async Task<Result<AuthResponse>> SignupAsync(SignupRequest request, CancellationToken ct = default)
    {
        // Normalize input (prevents annoying duplicates)
        var email = request.Email?.Trim().ToLowerInvariant();
        var username = request.Username?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username))
            return Result<AuthResponse>.Fail(new Error(
                ErrorType.Validation,
                "auth.invalid_input",
                "Email and Username are required."));

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return Result<AuthResponse>.Fail(new Error(
                ErrorType.Validation,
                "auth.password_too_short",
                "Password must be at least 8 characters."));

        // Conflicts
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Result<AuthResponse>.Fail(new Error(
                ErrorType.Conflict,
                "auth.email_exists",
                "Email already exists."));

        if (await db.Users.AnyAsync(u => u.Username == username, ct))
            return Result<AuthResponse>.Fail(new Error(
                ErrorType.Conflict,
                "auth.username_exists",
                "Username already exists."));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = hasher.HashPassword(request.Password)
        };

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Handles race conditions (two signups at the same time)
            return Result<AuthResponse>.Fail(new Error(
                ErrorType.Conflict,
                "auth.conflict",
                "User already exists."));
        }

        var token = tokenService.GenerateToken(user);
        return Result<AuthResponse>.Ok(new AuthResponse(token, user.Id, user.Username));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
            return Result<AuthResponse>.Fail(new Error(
                ErrorType.Validation,
                "auth.invalid_input",
                "Email and Password are required."));

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !hasher.VerifyPassword(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Fail(new Error(
                ErrorType.Unauthorized,
                "auth.invalid_credentials",
                "Invalid credentials."));

        var token = tokenService.GenerateToken(user);
        return Result<AuthResponse>.Ok(new AuthResponse(token, user.Id, user.Username));
    }
}