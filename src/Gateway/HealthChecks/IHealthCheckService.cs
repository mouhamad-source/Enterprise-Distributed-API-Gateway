namespace Gateway.HealthChecks;

public interface IHealthCheckService
{
    Task<HealthCheckResult> CheckAsync();
}