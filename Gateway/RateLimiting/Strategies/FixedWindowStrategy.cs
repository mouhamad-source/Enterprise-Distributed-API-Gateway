using StackExchange.Redis;

namespace Gateway.RateLimiting.Strategies;

public class FixedWindowStrategy : IRateLimitingStrategy
{
    public bool IsRequestAllowed(IDatabase redisDb, string key, int limit, TimeSpan window, out int currentCount)
    {
        var newCount = redisDb.StringIncrement(key);
        currentCount = (int)newCount;
        if (newCount == 1)
            redisDb.KeyExpire(key, window);
        return newCount <= limit;
    }

}