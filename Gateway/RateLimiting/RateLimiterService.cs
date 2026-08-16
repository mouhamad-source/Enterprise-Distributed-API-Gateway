using Gateway.Identification;
using Gateway.Services;
using Gateway.RateLimiting.Strategies;
using StackExchange.Redis;
using Gateway.Configuration;
using Gateway.Interface.RateLimiting;

namespace Gateway.RateLimiting;

public class RateLimiterService : IRateLimiter
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<RateLimiterService> _logger;
    private readonly IRateLimitingStrategy _strategy;
    private readonly int _limit;
    private readonly TimeSpan _window;

    public RateLimiterService(
        RedisConnectionManager redisManager,
        IConfiguration configuration,
        ILogger<RateLimiterService> logger)
    {
        _redisDb = redisManager.GetDatabase();
        _logger = logger;

        var rateConfig = configuration.GetSection("RateLimiting").Get<RateLimitingConfig>();
        _limit = rateConfig?.Limit ?? 100;
        _window = TimeSpan.FromSeconds(rateConfig?.WindowSeconds ?? 60);

        // اختيار الاستراتيجية بناءً على التكوين
        var algorithm = rateConfig?.Algorithm ?? "SlidingWindow";
        _strategy = algorithm switch
        {
            "FixedWindow" => new FixedWindowStrategy(),
            "SlidingWindow" => new SlidingWindowStrategy(),
            "TokenBucket" => new TokenBucketStrategy(),
            "LeakyBucket" => new LeakyBucketStrategy(),
            _ => new SlidingWindowStrategy()
        };
        _logger.LogInformation("Rate Limiter initialized with {Algorithm} algorithm", algorithm);
    }

    public bool IsRequestAllowed(ClientIdentifier clientId, out int currentCount)
    {
        var key = clientId.ToRedisKey(); // RateLimit:JWT:123
        try
        {
            return _strategy.IsRequestAllowed(_redisDb, key, _limit, _window, out currentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying rate limiting strategy for key {Key}", key);
            throw;
        }
    }
}