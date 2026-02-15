namespace Tripper.Application.Interfaces.Common;

public interface ICurrencyRateProvider
{
    Task<decimal> GetRateAsync(string baseCurrency, string targetCurrency, CancellationToken ct = default);
}