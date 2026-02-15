using Microsoft.Extensions.Caching.Memory;
using Tripper.Application.Interfaces.Common;

namespace Tripper.Infra.Currency;

public sealed class CachedCurrencyRateProvider(ICurrencyRateProvider inner, IMemoryCache cache, TimeSpan ttl) : ICurrencyRateProvider
{
    public Task<decimal> GetRateAsync(string from, string to, CancellationToken ct = default)
    {
        from = Normalize(from);
        to = Normalize(to);

        if (from == to) return Task.FromResult(1m);

        var key = $"fx:{from}:{to}";

        return cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            var rate = await inner.GetRateAsync(from, to, ct);
            return rate;
        })!;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}