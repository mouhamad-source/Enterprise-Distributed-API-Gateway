namespace Gateway.Configuration;

public class RateLimitingConfig
{
    public int Limit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public string Algorithm { get; set; } = "SlidingWindow";
}