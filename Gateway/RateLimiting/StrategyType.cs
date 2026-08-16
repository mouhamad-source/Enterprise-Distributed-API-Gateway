namespace Gateway.RateLimiting; 


public enum StrategyType
{
    FixedWindow,
    SlidingWindow,
    TokenBucket,
    LeakyBucket

}