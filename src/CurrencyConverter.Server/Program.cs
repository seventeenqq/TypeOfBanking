using System.Globalization;
using System.Text.Json;
using CurrencyConverter.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CbrRatesService>();

var app = builder.Build();

app.MapGet("/", () => Results.Content(
    "<html lang='ru'><meta charset='utf-8'><h1>Сервер конвертера валют</h1>" +
    "<p>GET /api/rates?date=2024-10-13</p>" +
    "<a href='https://www.cbr-xml-daily.ru/'>Курсы валют, API</a></html>", "text/html"));

app.MapGet("/api/rates", async (string? date, CbrRatesService service,
    CancellationToken cancellationToken) =>
{
    var todayInMoscow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).Date;
    if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var selectedDate) || selectedDate > todayInMoscow)
        return Results.BadRequest(new { error = "Нужна дата yyyy-MM-dd, не позднее сегодняшней." });

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(90));
    try
    {
        var rates = await service.GetRatesAsync(selectedDate, timeout.Token);
        return Results.Ok(rates);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = "Курсы не найдены." });
    }
    catch (OperationCanceledException)
    {
        return Results.Problem("Поиск занял слишком много времени. Повторите запрос.", statusCode: 504);
    }
    catch (Exception error) when (error is HttpRequestException or JsonException or InvalidDataException)
    {
        return Results.Problem("Источник курсов временно недоступен.", statusCode: 502);
    }
});

app.Run();
