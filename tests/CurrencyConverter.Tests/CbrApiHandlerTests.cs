using System.Net;
using System.Net.Http.Headers;
using CurrencyConverter.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CurrencyConverter.Tests;

public class CbrApiHandlerTests
{
    [Fact]
    public async Task NamedClientsShareLimitWithoutCallingPolicyManually()
    {
        using var transport = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var app = CreateApp(transport);
        var clients = app.Services.GetRequiredService<IHttpClientFactory>();
        using var first = clients.CreateClient("cbr");
        using var second = clients.CreateClient("cbr");

        using var response = await first.GetAsync("daily_json.js");
        await Assert.ThrowsAsync<CbrRateLimitException>(() => second.GetAsync("daily_json.js"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task UpstreamRateLimitPreservesRetryAfter()
    {
        using var transport = new StubHandler(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return response;
        });
        using var app = CreateApp(transport);
        using var client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("cbr");

        var error = await Assert.ThrowsAsync<CbrRateLimitException>(() => client.GetAsync("daily_json.js"));

        Assert.Equal(17, error.RetryAfterSeconds);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task RatesServiceChecksLimitOnceAndReusesCachedRates()
    {
        using var transport = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"Date":"2024-10-12T00:00:00+03:00","Valute":{
                    "USD":{"CharCode":"USD","Name":"Доллар США","Nominal":1,"Value":100}
                }}
                """)
        });
        using var app = CreateApp(transport);
        using var client = app.CreateClient();

        using var first = await client.GetAsync("/api/rates?date=2024-10-12");
        using var cached = await client.GetAsync("/api/rates?date=2024-10-12");
        using var limited = await client.GetAsync("/api/rates?date=2024-10-13");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(1, transport.Calls);
    }

    private static WebApplicationFactory<Program> CreateApp(StubHandler transport) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.Configure<CbrApiOptions>(options =>
                {
                    options.PeriodSeconds = 3600;
                    options.RequestsPerPeriod = 1;
                    options.RequestsPerDay = 100;
                });
                services.AddHttpClient("cbr").ConfigurePrimaryHttpMessageHandler(() => transport);
            }));

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond());
        }
    }
}
