using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gateway.Startup;

public class ResourceMonitor : BackgroundService
{
    private readonly ILogger<ResourceMonitor> _logger;
    private readonly IConfiguration _configuration;
    private readonly double _cpuThreshold;
    private readonly double _memoryThresholdMb;

    public ResourceMonitor(ILogger<ResourceMonitor> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _cpuThreshold = configuration.GetValue<double>("ResourceProtection:CpuThreshold", 80);
        _memoryThresholdMb = configuration.GetValue<double>("ResourceProtection:MemoryThresholdMb", 1024);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cpu = GetCpuUsage();
                var memory = GetMemoryUsageMb();

                if (cpu > _cpuThreshold)
                {
                    _logger.LogWarning("CPU usage high: {Cpu}% (threshold {Threshold}%)", cpu, _cpuThreshold);
                   
                }
                if (memory > _memoryThresholdMb)
                {
                    _logger.LogWarning("Memory usage high: {Memory}MB (threshold {Threshold}MB)", memory, _memoryThresholdMb);
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in resource monitor.");
            }
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            Thread.Sleep(100);
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