using Gateway.Configuration;
using Gateway.Identification;
using Gateway.Interface.RateLimiting;
using Gateway.Services ;
using StackExchange.Redis ; 


namespace Gateway.RateLimiting; 

public class RedisRateLimiter : IRateLimiter
{
    private readonly IDatabase _redisDb; 
    private readonly ILogger<RedisRateLimiter> _logger; 
    private readonly int _limit ; 
    private readonly TimeSpan _window; 
    private readonly string  _keyPrefix; 


    public RedisRateLimiter(
        RedisConnectionManager redisManager , 
        IConfiguration configuration , 
        ILogger<RedisRateLimiter> logger)
    {
        _redisDb =  redisManager.GetDatabase(); 
        _logger = logger; 
        var rateConfig = configuration.GetSection("RateLimiting").Get<RateLimitingConfig>();
        _limit = rateConfig?.Limit ?? 100; 
        _window = TimeSpan.FromSeconds(rateConfig?.WindowSeconds ?? 60);

        var redisConfig = configuration.GetSection("Redis").Get<RedisConfig>(); 
        _keyPrefix = redisConfig?.KeyPrefix ?? "RateLimit:"; 
    }

    [Obsolete]
    public bool IsRequestAllowed(ClientIdentifier clientId, out int currentCount)
    {
        return IsRequestAllowedSync(clientId , out currentCount);
    }

    [Obsolete]
    private bool IsRequestAllowedSync(ClientIdentifier clientId , out int currentCount)
    {
        var key = clientId.ToRedisKey(_keyPrefix); 
        try
        {
            var newCount = _redisDb.StringIncrement(key); 
            currentCount = (int)newCount; 

            if(newCount == 1)
            {
                _redisDb.KeyExpire(key , _window); 
            }

            return newCount <= _limit; 


            
        }catch(Exception ex)
        {
            _logger.LogError(ex, "Redis operation failed for key {Key}", key);
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis unavailable", ex);
        }
    }
}