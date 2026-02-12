using Microsoft.EntityFrameworkCore;
using Tripper.Core.DTOs;
using Tripper.Core.Entities;
using Tripper.Core.Interfaces;
using Tripper.Infra.Auth;
using Tripper.Infra.Data;

namespace Tripper.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/signup", async (SignupRequest request, TripperDbContext db, IPasswordHasher hasher, JwtTokenService jwt) =>
        {
            if (await db.Users.AnyAsync(u => u.Email == request.Email))
            {
                return Results.Conflict("Email already exists");
            }
            
            if (await db.Users.AnyAsync(u => u.Username == request.Username))
            {
                return Results.Conflict("Username already exists");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                PasswordHash = hasher.HashPassword(request.Password)
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var token = jwt.GenerateToken(user);
            return Results.Ok(new AuthResponse(token, user.Id, user.Username));
        });

        group.MapPost("/login", async (LoginRequest request, TripperDbContext db, IPasswordHasher hasher, JwtTokenService jwt) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !hasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var token = jwt.GenerateToken(user);
            return Results.Ok(new AuthResponse(token, user.Id, user.Username));
        });
    }
}
