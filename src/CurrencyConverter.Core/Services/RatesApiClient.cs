using System.Net.Http.Json;
using System.Globalization;
using CurrencyConverter.Core.Models;

namespace CurrencyConverter.Core.Services;

public class RatesApiClient : IRatesService
{
    private readonly HttpClient httpClient;

    public RatesApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<Rates> GetRatesAsync(DateTime date)
    {
        string dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var response = await httpClient.GetAsync($"api/rates?date={dateText}");

        response.EnsureSuccessStatusCode();

        var rates = await response.Content.ReadFromJsonAsync<Rates>();
        if (rates == null || rates.Currencies.Count == 0 || rates.Date.Date > date.Date ||
            rates.Currencies.Any(c => c.Nominal <= 0 || c.Value <= 0))
            throw new InvalidOperationException("Сервер вернул некорректные курсы.");

        return rates;
    }
}
