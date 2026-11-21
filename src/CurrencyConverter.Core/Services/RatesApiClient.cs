using System.Net.Http.Json;
using System.Globalization;
using CurrencyConverter.Core.Models;
using System.Text.Json;
using Polly.Timeout;

namespace CurrencyConverter.Core.Services;

public class RatesApiClient : IRatesService
{
    private readonly HttpClient httpClient;
    private readonly ApiRetryPolicy policy;

    public RatesApiClient(HttpClient httpClient, ApiRetryPolicy? policy = null)
    {
        this.httpClient = httpClient;
        this.policy = policy ?? new ApiRetryPolicy();
    }

    public async Task<Rates> GetRatesAsync(DateTime date)
    {
        string dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        int attempts = 0;
        HttpResponseMessage response;
        try
        {
            response = await policy.Pipeline.ExecuteAsync(async token =>
            {
                attempts++;
                return await httpClient.GetAsync($"api/rates?date={dateText}", token);
            });
        }
        catch (TimeoutRejectedException error)
        {
            throw new RatesApiException($"Превышено общее время ожидания. Выполнено попыток: {attempts}.", error);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            throw new RatesApiException($"Нет связи с сервером. Выполнено попыток: {attempts}.", error);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = "Сервер не смог получить курсы.";
                string? traceId = null;
                try
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (json.RootElement.TryGetProperty("detail", out var text)) detail = text.GetString() ?? detail;
                    if (json.RootElement.TryGetProperty("traceId", out var id)) traceId = id.GetString();
                }
                catch (JsonException) { /* Например, прокси вернул HTML вместо JSON. */ }
                throw new RatesApiException($"{detail} HTTP {(int)response.StatusCode}. " +
                    $"Попыток: {attempts}." + (traceId == null ? "" : $" Код запроса: {traceId}."));
            }

            var rates = await response.Content.ReadFromJsonAsync<Rates>();
            if (rates == null || rates.Currencies.Count == 0 || rates.Date.Date > date.Date ||
                rates.Currencies.Any(c => c.Nominal <= 0 || c.Value <= 0))
                throw new RatesApiException("Сервер вернул некорректные курсы.");
            return rates;
        }
    }
}
