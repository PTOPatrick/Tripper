using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Tripper.Infra.Data;

namespace Tripper.API.Endpoints;

public static class CurrencyEndpoints
{
    public static void MapCurrencyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/currencies").WithTags("Currencies");

        group.MapGet("/", async (TripperDbContext db, IMemoryCache cache, CancellationToken ct) =>
        {
            const string cacheKey = "currencies:all";
            if (cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
                return Results.Ok(cached);

            var codes = await db.Currencies
                .AsNoTracking()
                .Select(c => c.Code)
                .Where(code => code != "")
                .OrderBy(code => code)
                .ToListAsync(ct);

            cache.Set(cacheKey, codes, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6)
            });

            return Results.Ok(codes);
        });
    }
}