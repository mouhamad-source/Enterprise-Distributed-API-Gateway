namespace Gateway.Configuration;

public class RedisConfig
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string KeyPrefix { get; set; } = "RateLimit:";
}