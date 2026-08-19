using StackExchange.Redis;

namespace Gateway.RateLimiting.Strategies;

public class LeakyBucketStrategy : IRateLimitingStrategy
{
    private const string LuaScript = @"
        local key = KEYS[1]
        local now = tonumber(ARGV[1])
        local rate = tonumber(ARGV[2])      
        local capacity = tonumber(ARGV[3])  
        local requested = tonumber(ARGV[4])
        
        local water_key = key .. ':water'
        local last_leave_key = key .. ':last'
        
        local last_leave = redis.call('GET', last_leave_key)
        if not last_leave then
            last_leave = now
            redis.call('SET', water_key, 0)
            redis.call('SET', last_leave_key, now)
        end
        
        local elapsed = now - tonumber(last_leave)
        local leaked = elapsed * rate
        local current_water = math.max(0, tonumber(redis.call('GET', water_key) or 0) - leaked)
        
        if current_water + requested <= capacity then
            local new_water = current_water + requested
            redis.call('SET', water_key, new_water)
            redis.call('SET', last_leave_key, now)
            redis.call('EXPIRE', water_key, 60)
            redis.call('EXPIRE', last_leave_key, 60)
            return {1, new_water}
        else
            return {0, current_water}
        end
    ";

    public bool IsRequestAllowed(IDatabase redisDb, string key, int limit, TimeSpan window, out int currentCount)
    {
        var nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rate = limit / window.TotalSeconds; 
        var capacity = limit;

        var result = (RedisResult[]) redisDb.ScriptEvaluate(
            LuaScript,
            new RedisKey[] { key },
            new RedisValue[] { nowSec, rate, capacity, 1 }
        );  

        var allowed = (int)result[0] == 1;
        currentCount = (int)Math.Ceiling((double)result[1]);
        return allowed;
    }
}