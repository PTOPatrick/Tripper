namespace Tripper.Infra.Options;

public class ExchangeRateOptions
{
    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "https://v6.exchangerate-api.com/v6/";
}