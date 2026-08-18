namespace Gateway.HealthChecks;

public class HealthCheckResult
{
    public string Status { get; set; } = "Healthy"; // Healthy, Degraded, Unhealthy
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<ComponentHealth> Components { get; set; } = new();

    public class ComponentHealth
    {
        public string Component { get; set; } = string.Empty;
        public string Status { get; set; } = "Healthy";
        public double LatencyMs { get; set; }
        public string? Error { get; set; }
    }
}