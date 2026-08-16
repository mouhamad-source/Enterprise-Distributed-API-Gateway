namespace Gateway.Resilience;

public class ResilienceConfig
{
    public int TimeoutSeconds { get; set; } = 2;
    public int RetryCount { get; set; } = 3;
    public int BaseDelayMilliseconds { get; set; } = 100;
    public int BreakDurationSeconds { get; set; } = 30;
    public int ExceptionsAllowedBeforeBreaking { get; set; } = 5;
}