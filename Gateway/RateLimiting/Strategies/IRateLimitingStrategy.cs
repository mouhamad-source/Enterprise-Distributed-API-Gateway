using StackExchange.Redis; 

namespace Gateway.RateLimiting.Strategies; 

public interface IRateLimitingStrategy
{
    bool IsRequestAllowed(IDatabase redisDb , string key , int limit, TimeSpan window , out int currentCount); 
    
}