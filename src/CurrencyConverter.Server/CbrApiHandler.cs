using System.Net;

namespace CurrencyConverter.Server;

public sealed class CbrApiHandler(CbrApiPolicy policy) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Общая политика применяется к каждому запросу именованного клиента cbr.
        policy.Check();

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var header = response.Headers.RetryAfter;
            var delay = header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow)
                ?? TimeSpan.FromSeconds(3);
            response.Dispose();
            throw new CbrRateLimitException(delay);
        }

        return response;
    }
}
