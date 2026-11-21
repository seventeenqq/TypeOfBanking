using System.Net;
using Polly;
using Polly.Retry;

namespace CurrencyConverter.Core.Services;

public class ApiRetryPolicy
{
    public ResiliencePipeline<HttpResponseMessage> Pipeline { get; }

    public ApiRetryPolicy(int maxAttempts = 5, TimeSpan? delay = null, TimeSpan? timeout = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(timeout ?? TimeSpan.FromSeconds(120));

        if (maxAttempts > 1)
        {
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = maxAttempts - 1,
                Delay = delay ?? TimeSpan.FromSeconds(3),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests ||
                        r.StatusCode == HttpStatusCode.RequestTimeout || (int)r.StatusCode >= 500),
                DelayGenerator = args => new ValueTask<TimeSpan?>(GetRetryDelay(args.Outcome.Result)),
                OnRetry = args =>
                {
                    args.Outcome.Result?.Dispose();
                    return default;
                }
            });
        }
        Pipeline = builder.Build();
    }

    private static TimeSpan? GetRetryDelay(HttpResponseMessage? response)
    {
        var header = response?.Headers.RetryAfter;
        TimeSpan? delay = header?.Delta;
        if (header?.Date != null) delay = header.Date.Value - DateTimeOffset.UtcNow;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }
}
