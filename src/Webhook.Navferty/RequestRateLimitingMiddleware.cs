using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Webhook.Navferty;

public sealed class RequestRateLimitingOptions
{
    public const string SectionName = "RequestRateLimiting";
    public required int RequestsPerMinute { get; set; } = 60;
}

public sealed class RequestRateLimitingMiddleware(
    RequestDelegate next,
    IMemoryCache cache,
    TimeProvider timeProvider,
    IOptions<RequestRateLimitingOptions> options,
    ILogger<RequestRateLimitingMiddleware> logger)
{

    public async Task InvokeAsync(HttpContext context)
    {
        var limit = options.Value.RequestsPerMinute;

        if (limit <= 0)
        {
            await next(context);
            return;
        }

        var cacheKey = GetKey(context);

        if (string.IsNullOrEmpty(cacheKey))
        {
            await next(context);
            return;
        }

        // Use a boxed int for atomic operations
        var counter = cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return new StrongBox<int>(0);
        });

        int newCount = Interlocked.Increment(ref counter!.Value);

        if (newCount > limit)
        {
            logger.LogWarning("Rate limit exceeded for {CacheKey}. Requests: {Requests}, Limit: {Limit}", cacheKey, newCount, limit);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Too many requests. Please try again later.");
            return;
        }

        await next(context);
    }

    private string? GetKey(HttpContext context)
    {
        var ip = context.Request.Headers["X-Real-IP"].FirstOrDefault()
            ?? context.Connection.RemoteIpAddress?.ToString();

        // No IP address found, skip rate limiting
        if (string.IsNullOrEmpty(ip))
            return null;

        var now = timeProvider.GetUtcNow();
        var cacheKey = $"rl:{ip}:{now:yyyyMMddHHmm}";
        return cacheKey;
    }
}
