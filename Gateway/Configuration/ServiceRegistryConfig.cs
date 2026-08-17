namespace Gateway.Configuration;

public class ServiceRegistryConfig
{
    public Dictionary<string, ServiceConfig> Services { get; set; } = new();
}

public class ServiceConfig
{
    public List<string> Instances { get; set; } = new();
    public int HealthCheckIntervalSeconds { get; set; } = 10;
}