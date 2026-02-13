using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tripper.Infra.Data;

namespace Tripper.Infra;

public static class DependencyInjection
{
    public static IServiceCollection AddInfra(this IServiceCollection services, string dbConnectionString)
    {
        services.AddDbContext<TripperDbContext>(options => options.UseNpgsql(dbConnectionString));
        return services;
    }
}