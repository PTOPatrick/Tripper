using Microsoft.Extensions.DependencyInjection;
using Tripper.Application.Interfaces;
using Tripper.Application.Services;

namespace Tripper.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IVotingService, VotingService>();
        return services;
    }
}