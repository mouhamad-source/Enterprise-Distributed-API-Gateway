using System.Diagnostics;
using Gateway.RateLimiting; 
using Gateway.ServiceDiscovery;
using Gateway.Services;
using StackExchange.Redis;

namespace Gateway.HealthChecks;

public class HealthCheckService : IHealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly RedisConnectionManager _redisManager;
    private readonly IServiceRegistry _serviceRegistry;
    private readonly IConfiguration _configuration;

    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        RedisConnectionManager redisManager,
        IServiceRegistry serviceRegistry,
        IConfiguration configuration)
    {
        _logger = logger;
        _redisManager = redisManager;
        _serviceRegistry = serviceRegistry;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckAsync()
    {
        var result = new HealthCheckResult();
        var tasks = new List<Task<HealthCheckResult.ComponentHealth>>();

        // 1. Redis
        tasks.Add(CheckRedisAsync());

        // 2. Service Registry (UserService instances)
        tasks.Add(CheckUserServiceAsync());

        // 3. Configuration (optional)
        tasks.Add(CheckConfigurationAsync());

        var components = await Task.WhenAll(tasks);
        result.Components.AddRange(components);

        
        var unhealthy = result.Components.Any(c => c.Status == "Unhealthy");
        var degraded = result.Components.Any(c => c.Status == "Degraded");
        result.Status = unhealthy ? "Unhealthy" : degraded ? "Degraded" : "Healthy";

        return result;
    }

    private async Task<HealthCheckResult.ComponentHealth> CheckRedisAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var db = _redisManager.GetDatabase();
            await db.PingAsync();
            sw.Stop();
            return new HealthCheckResult.ComponentHealth
            {
                Component = "Redis",
                Status = "Healthy",
                LatencyMs = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Redis health check failed.");
            return new HealthCheckResult.ComponentHealth
            {
                Component = "Redis",
                Status = "Unhealthy",
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<HealthCheckResult.ComponentHealth> CheckUserServiceAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            
            var instances = await _serviceRegistry.GetInstancesAsync("UserService");
            if (instances == null || instances.Count == 0)
            {
                return new HealthCheckResult.ComponentHealth
                {
                    Component = "UserService",
                    Status = "Unhealthy",
                    LatencyMs = sw.Elapsed.TotalMilliseconds,
                    Error = "No instances available"
                };
            }

           
            var instance = instances.First();
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(2);
            var response = await http.GetAsync($"{instance.TrimEnd('/')}/health");
            sw.Stop();
            if (response.IsSuccessStatusCode)
            {
                return new HealthCheckResult.ComponentHealth
                {
                    Component = "UserService",
                    Status = "Healthy",
                    LatencyMs = sw.Elapsed.TotalMilliseconds
                };
            }
            else
            {
                return new HealthCheckResult.ComponentHealth
                {
                    Component = "UserService",
                    Status = "Degraded",
                    LatencyMs = sw.Elapsed.TotalMilliseconds,
                    Error = $"Health check returned {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new HealthCheckResult.ComponentHealth
            {
                Component = "UserService",
                Status = "Unhealthy",
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                Error = ex.Message
            };
        }
    }

    private Task<HealthCheckResult.ComponentHealth> CheckConfigurationAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
           
            var routes = _configuration.GetSection("Routes").Get<Dictionary<string, string>>();
            if (routes == null || !routes.ContainsKey("/users"))
            {
                return Task.FromResult(new HealthCheckResult.ComponentHealth
                {
                    Component = "Configuration",
                    Status = "Degraded",
                    LatencyMs = sw.Elapsed.TotalMilliseconds,
                    Error = "Routes configuration missing"
                });
            }
            sw.Stop();
            return Task.FromResult(new HealthCheckResult.ComponentHealth
            {
                Component = "Configuration",
                Status = "Healthy",
                LatencyMs = sw.Elapsed.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Task.FromResult(new HealthCheckResult.ComponentHealth
            {
                Component = "Configuration",
                Status = "Unhealthy",
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                Error = ex.Message
            });
        }
    }
}