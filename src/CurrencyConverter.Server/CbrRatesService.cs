using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CurrencyConverter.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CurrencyConverter.Server;

public class CbrRatesService
{
    private readonly IHttpClientFactory clients;
    private readonly IMemoryCache cache;
    private readonly IHostApplicationLifetime lifetime;
    private readonly ConcurrentDictionary<string, Lazy<Task<CbrResponse?>>> loading = new();

    public CbrRatesService(IHttpClientFactory clients, IMemoryCache cache,
        IHostApplicationLifetime lifetime)
    {
        this.clients = clients;
        this.cache = cache;
        this.lifetime = lifetime;
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

        var task = loading.GetOrAdd(key,
            _ => new Lazy<Task<CbrResponse?>>(() => LoadDayAsync(date, key)));
        return await task.Value.WaitAsync(token);
    }

    private async Task<CbrResponse?> LoadDayAsync(DateTime date, string key)
    {
        string path = "archive/" + date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
            + "/daily_json.js";
        try
        {
            if (cache.TryGetValue(key, out CbrResponse? saved)) return saved;
            using var client = clients.CreateClient("cbr");
            using var response = await client.GetAsync(path, lifetime.ApplicationStopping);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                cache.Set<CbrResponse?>(key, null, TimeSpan.FromHours(1));
                return null;
            }

            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<CbrResponse>(lifetime.ApplicationStopping);
            if (data == null || data.Date == default || data.Date.Date > date ||
                data.Valute.Count == 0 ||
                data.Valute.Values.Any(c => c.Nominal <= 0 || c.Value <= 0 ||
                    string.IsNullOrWhiteSpace(c.CharCode) || string.IsNullOrWhiteSpace(c.Name)))
                throw new InvalidDataException("Некорректный ответ ЦБ");

            cache.Set(key, data, TimeSpan.FromHours(12));
            return data;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidDataException)
        {
            throw new CbrSourceException(date, path, error);
        }
        catch (OperationCanceledException error) when (!lifetime.ApplicationStopping.IsCancellationRequested)
        {
            throw new CbrSourceException(date, path, error);
        }
        finally
        {
            loading.TryRemove(key, out _);
        }
    }
}

public class CbrSourceException : Exception
{
    public CbrSourceException(DateTime date, string path, Exception innerException)
        : base($"Ошибка источника за {date:yyyy-MM-dd}: https://www.cbr-xml-daily.ru/{path}", innerException) { }
}
