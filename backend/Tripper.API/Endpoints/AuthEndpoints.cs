using Tripper.API.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Application.Interfaces.Services;

namespace Tripper.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/signup", async (SignupRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.SignupAsync(request, ct);
            return result.ToHttpResult();
        });

        group.MapPost("/login", async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.LoginAsync(request, ct);
            return result.ToHttpResult();
        });
    }
}
