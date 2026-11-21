using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using CurrencyConverter.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CurrencyConverter.Server;

public class CbrRatesService
{
    private readonly HttpClient httpClient = new()
    {
        BaseAddress = new Uri("https://www.cbr-xml-daily.ru/"),
        Timeout = TimeSpan.FromSeconds(20)
    };
    private readonly IMemoryCache cache;
    private readonly SemaphoreSlim requestLock = new(1, 1);
    private DateTime nextRequest = DateTime.MinValue;

    public CbrRatesService(IMemoryCache cache)
    {
        this.cache = cache;
    }

    public async Task<Rates> GetRatesAsync(DateTime selectedDate, CancellationToken token)
    {
        var date = selectedDate.Date;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var response = await GetDayAsync(date, token);
            if (response != null)
            {
                var result = new Rates { Date = response.Date.Date };
                result.Currencies.Add(new Currency
                {
                    Code = "RUB", Name = "Российский рубль", Nominal = 1, Value = 1
                });
                foreach (var item in response.Valute.Values)
                {
                    result.Currencies.Add(new Currency
                    {
                        Code = item.CharCode, Name = item.Name,
                        Nominal = item.Nominal, Value = item.Value
                    });
                }
                return result;
            }

            if (date == DateTime.MinValue.Date) break;
            date = date.AddDays(-1);
        }
        throw new KeyNotFoundException("Курсы не найдены.");
    }

    private async Task<CbrResponse?> GetDayAsync(DateTime date, CancellationToken token)
    {
        string key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (cache.TryGetValue(key, out CbrResponse? saved)) return saved;

        await requestLock.WaitAsync(token);
        try
        {
            if (cache.TryGetValue(key, out saved)) return saved;

            var delay = nextRequest - DateTime.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
            nextRequest = DateTime.UtcNow.AddSeconds(2.1);

            string path = "archive/" + date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
                + "/daily_json.js";
            using var response = await httpClient.GetAsync(path, token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                cache.Set<CbrResponse?>(key, null, TimeSpan.FromHours(1));
                return null;
            }

            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<CbrResponse>(token);
            if (data == null || data.Date == default || data.Date.Date > date ||
                data.Valute.Count == 0 ||
                data.Valute.Values.Any(c => c.Nominal <= 0 || c.Value <= 0 ||
                    string.IsNullOrWhiteSpace(c.CharCode) || string.IsNullOrWhiteSpace(c.Name)))
                throw new InvalidDataException("Некорректный ответ ЦБ");

            cache.Set(key, data, TimeSpan.FromHours(12));
            return data;
        }
        finally
        {
            requestLock.Release();
        }
    }
}
