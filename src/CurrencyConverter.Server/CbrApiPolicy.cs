using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace CurrencyConverter.Server;

public class CbrApiOptions
{
    public double PeriodSeconds { get; set; } = 2.1;
    public int RequestsPerPeriod { get; set; } = 1;
    public int RequestsPerDay { get; set; } = 10000;
}

public class CbrApiPolicy : IDisposable
{
    private readonly SlidingWindowRateLimiter shortLimit;
    private readonly FixedWindowRateLimiter dailyLimit;
    private readonly TimeSpan period;

    public CbrApiPolicy(IOptions<CbrApiOptions> options)
    {
        var settings = options.Value;
        period = TimeSpan.FromSeconds(settings.PeriodSeconds);
        shortLimit = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = settings.RequestsPerPeriod,
            Window = period,
            SegmentsPerWindow = 21,
            QueueLimit = 0,
            AutoReplenishment = true
        });
        dailyLimit = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.RequestsPerDay,
            Window = TimeSpan.FromDays(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    public void Check()
    {
        using var shortLease = shortLimit.AttemptAcquire();
        if (!shortLease.IsAcquired) throw new CbrRateLimitException(period);
        using var dailyLease = dailyLimit.AttemptAcquire();
        if (!dailyLease.IsAcquired)
        {
            var delay = dailyLease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? retryAfter : TimeSpan.FromDays(1);
            throw new CbrRateLimitException(delay);
        }
    }

    public void Dispose()
    {
        shortLimit.Dispose();
        dailyLimit.Dispose();
    }
}

public class CbrRateLimitException : Exception
{
    public int RetryAfterSeconds { get; }

    public CbrRateLimitException(TimeSpan delay) : base("Достигнут лимит запросов к источнику курсов.")
    {
        RetryAfterSeconds = Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));
    }
}
