using System.Globalization;
using CurrencyConverter.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();
builder.Services.Configure<CbrApiOptions>(builder.Configuration.GetSection("CbrApi"));
builder.Services.AddSingleton<CbrApiPolicy>();
builder.Services.AddTransient<CbrApiHandler>();
builder.Services.AddSingleton<CbrRatesService>();
builder.Services.AddHttpClient("cbr", client =>
{
    client.BaseAddress = new Uri("https://www.cbr-xml-daily.ru/");
    client.Timeout = TimeSpan.FromSeconds(20);
}).AddHttpMessageHandler<CbrApiHandler>();

var app = builder.Build();

app.MapGet("/", () => Results.Content(
    "<html lang='ru'><meta charset='utf-8'><h1>Сервер конвертера валют</h1>" +
    "<p>GET /api/rates?date=2024-10-13</p>" +
    "<a href='https://www.cbr-xml-daily.ru/'>Курсы валют, API</a></html>", "text/html"));

app.MapGet("/api/rates", async (string? date, CbrRatesService service, HttpContext context,
    CancellationToken cancellationToken) =>
{
    string requestId = context.TraceIdentifier;
    using var scope = app.Logger.BeginScope("RequestId={RequestId}, Date={Date}", requestId, date);
    IResult Problem(string message, int status) => Results.Problem(
        detail: message, statusCode: status,
        extensions: new Dictionary<string, object?> { ["traceId"] = requestId });

    var todayInMoscow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).Date;
    if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var selectedDate) || selectedDate > todayInMoscow)
        return Problem("Нужна дата yyyy-MM-dd, не позднее сегодняшней.", 400);

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(90));
    try
    {
        return Results.Ok(await service.GetRatesAsync(selectedDate, timeout.Token));
    }
    catch (CbrRateLimitException error)
    {
        context.Response.Headers.RetryAfter = error.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        app.Logger.LogWarning("Лимит API. Дата {Date}, повтор через {Seconds} с. Запрос {RequestId}",
            date, error.RetryAfterSeconds, requestId);
        return Problem("Достигнут лимит запросов к источнику курсов. Попробуйте позже.", 429);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        app.Logger.LogInformation("Клиент отменил запрос {RequestId} за {Date}", requestId, date);
        return Results.StatusCode(499);
    }
    catch (OperationCanceledException error)
    {
        app.Logger.LogWarning(error, "Истекло время загрузки за {Date}. Запрос {RequestId}", date, requestId);
        return Problem("Превышено время ожидания курсов.", 504);
    }
    catch (KeyNotFoundException)
    {
        return Problem("Для выбранной даты и предыдущих дней курсы не найдены.", 404);
    }
    catch (CbrSourceException error)
    {
        app.Logger.LogError(error, "Не удалось получить курсы за {Date}. Запрос {RequestId}", date, requestId);
        if (error.InnerException is OperationCanceledException)
            return Problem("Источник курсов не ответил вовремя.", 504);
        return Problem("Источник курсов вернул ошибку или некорректные данные.", 502);
    }
    catch (Exception error)
    {
        app.Logger.LogError(error, "Неожиданная ошибка за {Date}. Запрос {RequestId}", date, requestId);
        return Problem("Внутренняя ошибка сервера.", 500);
    }
});

app.Run();
public partial class Program { }
