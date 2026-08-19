using Gateway.Authentication;
using Gateway.Configuration;
using Gateway.Identification;
using Gateway.Interface.RateLimiting;
using Gateway.RateLimiting.Strategies;
using Gateway.Services;
using StackExchange.Redis;

namespace Gateway.RateLimiting;

public class RateLimiterService : IRateLimiter
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<RateLimiterService> _logger;
    private readonly IRateLimitingStrategy _strategy;
    private readonly int _defaultLimit;
    private readonly int _windowSeconds;
    private readonly TimeSpan _window;
    private readonly string _keyPrefix;

    public RateLimiterService(
        RedisConnectionManager redisManager,
        IConfiguration configuration,
        ILogger<RateLimiterService> logger)
    {
        _redisDb = redisManager.GetDatabase();
        _logger = logger;

        var rateConfig = configuration.GetSection("RateLimiting").Get<RateLimitingConfig>();
        _defaultLimit = rateConfig?.DefaultLimit ?? 100;
        _windowSeconds = rateConfig?.WindowSeconds ?? 60;
        _window = TimeSpan.FromSeconds(_windowSeconds);

        var redisConfig = configuration.GetSection("Redis").Get<RedisConfig>();
        _keyPrefix = redisConfig?.KeyPrefix ?? "RateLimit:";

        var algorithm = rateConfig?.Algorithm ?? "SlidingWindow";
        _strategy = algorithm switch
        {
            "FixedWindow" => new FixedWindowStrategy(),
            "SlidingWindow" => new SlidingWindowStrategy(),
            "TokenBucket" => new TokenBucketStrategy(),
            "LeakyBucket" => new LeakyBucketStrategy(),
            _ => new SlidingWindowStrategy()
        };
        _logger.LogInformation("Rate Limiter initialized with {Algorithm} algorithm.", algorithm);
    }

    public bool IsRequestAllowed(ClientIdentifier clientId, UserContext? userContext, out int currentCount)
    {
        var limit = GetLimitForUser(userContext);
        var key = clientId.ToRedisKey(_keyPrefix);

        
        if (userContext?.Role == "Admin")
        {
            currentCount = 0;
            _logger.LogDebug("Admin user {UserId} bypassed rate limit.", userContext.UserId);
            return true;
        }

        try
        {
            return _strategy.IsRequestAllowed(_redisDb, key, limit, _window, out currentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying rate limiting strategy for key {Key}", key);
            throw;
        }
    }

    private int GetLimitForUser(UserContext? userContext)
    {
        if (userContext == null)
            return _defaultLimit;

        return userContext.Plan switch
        {
            "Free" => 100,
            "Premium" => 5000,
            "Admin" => int.MaxValue, 
            _ => _defaultLimit
        };
    }
}