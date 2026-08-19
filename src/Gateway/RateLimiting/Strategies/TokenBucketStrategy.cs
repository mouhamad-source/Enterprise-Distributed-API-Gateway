using StackExchange.Redis;

namespace Gateway.RateLimiting.Strategies;

public class TokenBucketStrategy : IRateLimitingStrategy
{
    private const string LuaScript = @"
        local key = KEYS[1]
        local now = tonumber(ARGV[1])
        local rate = tonumber(ARGV[2])      -- عدد الرموز المضافة في الثانية
        local capacity = tonumber(ARGV[3])  -- السعة القصوى
        local requested = tonumber(ARGV[4]) -- عدد الرموز المطلوبة (عادة 1)
        
        local tokens_key = key .. ':tokens'
        local last_refill_key = key .. ':last'
        
        local last_refill = redis.call('GET', last_refill_key)
        if not last_refill then
            last_refill = now
            redis.call('SET', tokens_key, capacity)
            redis.call('SET', last_refill_key, now)
        end
        
        local elapsed = now - tonumber(last_refill)
        local refill = elapsed * rate
        local current_tokens = math.min(tonumber(redis.call('GET', tokens_key) or capacity), capacity)
        current_tokens = math.min(capacity, current_tokens + refill)
        
        if current_tokens >= requested then
            local new_tokens = current_tokens - requested
            redis.call('SET', tokens_key, new_tokens)
            redis.call('SET', last_refill_key, now)
            redis.call('EXPIRE', tokens_key, 60)
            redis.call('EXPIRE', last_refill_key, 60)
            return {1, new_tokens}
        else
            return {0, current_tokens}
        end
    ";

    public bool IsRequestAllowed(IDatabase redisDb, string key, int limit, TimeSpan window, out int currentCount)
    {
        var nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rate = limit / window.TotalSeconds; // مثلاً 100 طلب / 60 ثانية = 1.66 رمز/ثانية
        var capacity = limit;

        var result = (RedisResult[])redisDb.ScriptEvaluate(
            LuaScript,
            new RedisKey[] { key },
            new RedisValue[] { nowSec, rate, capacity, 1 }
        );

        var allowed = (int)result[0] == 1;
        currentCount = (int)Math.Ceiling((double)result[1]); // الرموز المتبقية كعدد صحيح
        return allowed;
    }
}