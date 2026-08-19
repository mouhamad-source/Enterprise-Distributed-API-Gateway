using Gateway.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gateway.Startup;

public class StartupValidator : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StartupValidator> _logger;

    public StartupValidator(IServiceProvider serviceProvider, ILogger<StartupValidator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running startup validation...");
        using var scope = _serviceProvider.CreateScope();
        var healthCheck = scope.ServiceProvider.GetRequiredService<IHealthCheckService>();

        var result = await healthCheck.CheckAsync();
        if (result.Status == "Unhealthy")
        {
            _logger.LogCritical("Startup validation failed: one or more dependencies are unhealthy.");
            
            Environment.Exit(1);
        }
        else if (result.Status == "Degraded")
        {
            _logger.LogWarning("Startup validation: some dependencies are degraded, but system can start.");
        }
        else
        {
            _logger.LogInformation("Startup validation passed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}