using StackExchange.Redis;

namespace Gateway.RateLimiting.Strategies;


public class SlidingWindowStrategy : IRateLimitingStrategy
{
    private const string LuaScript = @"
        local key = KEYS[1]
        local now = tonumber(ARGV[1])
        local window_ms = tonumber(ARGV[2])
        local limit = tonumber(ARGV[3])
        
        -- حذف الطلبات الأقدم من النافذة
        local window_start = now - window_ms
        redis.call('ZREMRANGEBYSCORE', key, 0, window_start)
        
        -- الحصول على العدد الحالي
        local current_count = redis.call('ZCARD', key)
        
        if current_count < limit then
            -- إضافة الطلب الجديد مع طابع زمني فريد (نستخدم now + العداد لضمان تفرد العضو)
            redis.call('ZADD', key, now, now .. '_' .. current_count)
            redis.call('EXPIRE', key, math.ceil(window_ms / 1000) + 1)
            return {1, current_count + 1}
        else
            return {0, current_count}
        end
    ";

    private readonly RedisResult _cachedScriptHash;

    public SlidingWindowStrategy()
    {
        _cachedScriptHash = null!;
    }

    public bool IsRequestAllowed(IDatabase redisDb, string key, int limit, TimeSpan window, out int currentCount)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowMs = window.TotalMilliseconds;

        // تنفيذ Lua script
        var result = (RedisResult[])redisDb.ScriptEvaluate(
            LuaScript,
            new RedisKey[] { key },
            new RedisValue[] { nowMs, windowMs, limit }
        );

        var allowed = (int)result[0] == 1;
        currentCount = (int)result[1];
        return allowed;
    }

}