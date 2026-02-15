using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Tripper.Application.Interfaces.Common;
using Tripper.Infra.Options;

namespace Tripper.Infra.Currency;

public sealed class ExchangeRateHostProvider(HttpClient http, IOptions<ExchangeRateOptions> options)
    : ICurrencyRateProvider
{
    private readonly ExchangeRateOptions _options = options.Value;

    public async Task<decimal> GetRateAsync(string from, string to, CancellationToken ct = default)
    {
        from = Normalize(from);
        to   = Normalize(to);

        if (from == to) return 1m;

        // https://v6.exchangerate-api.com/v6/{key}/latest/USD
        var url = $"{_options.ApiKey}/latest/{from}";

        var response = await http.GetFromJsonAsync<LatestResponse>(url, ct);
        if (response is null)
            throw new InvalidOperationException("ExchangeRate API returned no response.");

        if (!string.Equals(response.Result, "success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(response.ErrorType ?? "exchange_rate_error");

        if (response.ConversionRates is null || !response.ConversionRates.TryGetValue(to, out var rate))
            throw new InvalidOperationException($"rate_not_found: {from}->{to}");

        return rate;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private sealed class LatestResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; } // "success"

        [JsonPropertyName("error-type")]
        public string? ErrorType { get; set; } // when failed

        [JsonPropertyName("base_code")]
        public string? BaseCode { get; set; }

        [JsonPropertyName("conversion_rates")]
        public Dictionary<string, decimal>? ConversionRates { get; set; }
    }
}