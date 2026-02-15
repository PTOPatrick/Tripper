using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tripper.Application.Interfaces.Common;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Infra.Currency;
using Tripper.Infra.Data;
using Tripper.Infra.Options;
using Tripper.Infra.Repositories;

namespace Tripper.Infra;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfra(IConfiguration configuration)
        {
            
            var dbConnectionString = configuration.GetConnectionString("DefaultConnection")!;
            services.AddDbContext<TripperDbContext>(options => options.UseNpgsql(dbConnectionString));
            services
                .AddRepositories()
                .AddCurrencyServices(configuration);
            return services;
        }

        private IServiceCollection AddRepositories()
        {
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IGroupRepository, GroupRepository>();
            services.AddScoped<IUserLookupRepository, UserLookupRepository>();
            services.AddScoped<IItemRepository, ItemRepository>();
            services.AddScoped<IVotingRepository, VotingRepository>();

            return services;
        }
        
        private IServiceCollection AddCurrencyServices(IConfiguration configuration)
        {
            services.Configure<ExchangeRateOptions>(configuration.GetSection("ExchangeRates"));

            services.AddMemoryCache();

            services.AddHttpClient<ExchangeRateHostProvider>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<ExchangeRateOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            services.AddScoped<ICurrencyRateProvider>(sp =>
            {
                var inner = sp.GetRequiredService<ExchangeRateHostProvider>();
                var cache = sp.GetRequiredService<IMemoryCache>();
                return new CachedCurrencyRateProvider(inner, cache, TimeSpan.FromMinutes(30));
            });

            return services;
        }
    }
}