using System.Diagnostics;
using System.IO;  // Make sure this is included
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Gateway.HealthChecks;
using Gateway.Configuration;


namespace Gateway.Controllers;


[ApiController]
[Route("ops")]
public class OpsController : ControllerBase
{
    private readonly IHealthCheckService _healthCheck;
    private readonly IFeatureFlagProvider _featureFlag;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;


    public OpsController(
        IHealthCheckService healthCheck,
        IFeatureFlagProvider featureFlag,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        _healthCheck = healthCheck;
        _featureFlag = featureFlag;
        _configuration = configuration;
        _env = env;
    }


    [HttpGet]
    public async Task<IActionResult> GetOps()
    {
        var health = await _healthCheck.CheckAsync();
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        var buildTime = System.IO.File.GetLastWriteTime(Assembly.GetEntryAssembly()?.Location ?? "");  // Fixed: fully qualified


        var result = new
        {
            Status = health.Status,
            Version = version,
            BuildTime = buildTime,
            Environment = _env.EnvironmentName,
            InstanceId = Environment.MachineName,
            Health = health,
            Features = new
            {
                SlidingWindow = _featureFlag.IsEnabled("SlidingWindow"),
                EnableTracing = _featureFlag.IsEnabled("EnableTracing"),
                UseCircuitBreaker = _featureFlag.IsEnabled("UseCircuitBreaker")
            },
            Configuration = new
            {
                RateLimit = _configuration.GetValue<int>("RateLimiting:Limit"),
                Timeout = _configuration.GetValue<int>("Resilience:TimeoutSeconds"),
                RedisConnection = _configuration.GetValue<string>("Redis:ConnectionString")
            },
            Process = new
            {
                CPU = GetCpuUsage(),
                MemoryMB = GetMemoryUsageMb(),
                Threads = Process.GetCurrentProcess().Threads.Count,
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
            }
        };


        return Ok(result);
    }


    private double GetCpuUsage()
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            Thread.Sleep(50);
            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMs = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMs);
            return cpuUsageTotal * 100;
        }
        catch
        {
            return 0;
        }
    }


    private double GetMemoryUsageMb()
    {
        try
        {
            return Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
        }
        catch
        {
            return 0;
        }
    }
}