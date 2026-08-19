using System.Collections.Concurrent;
using System.Net.Http;
using Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace Gateway.ServiceDiscovery;

public class InMemoryServiceRegistry : IServiceRegistry, IDisposable
{
    private readonly ILogger<InMemoryServiceRegistry> _logger;
    private readonly ServiceRegistryConfig _config;
    private readonly HttpClient _httpClient;

    private readonly ConcurrentDictionary<string, List<string>> _instances = new();
    private Timer? _timer;
    private bool _disposed;

    public InMemoryServiceRegistry(
        IOptions<ServiceRegistryConfig> options,
        ILogger<InMemoryServiceRegistry> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = options.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("HealthCheckClient");
        _httpClient.Timeout = TimeSpan.FromSeconds(2);

        
        

        
        foreach (var service in _config.Services)
        {
            _instances[service.Key] = new List<string>(service.Value.Instances);
            
        }
    }

    public Task<IReadOnlyList<string>> GetInstancesAsync(string serviceName)
    {
        if (_instances.TryGetValue(serviceName, out var list) && list.Count > 0)
        {
           

            // Return all instances; the middleware will use ILoadBalancer to select one.
            return Task.FromResult<IReadOnlyList<string>>(list.AsReadOnly());
        }

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Run health checks every 10 seconds
        _timer = new Timer(UpdateInstances, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    private async void UpdateInstances(object? state)
    {
        try
        {
            foreach (var service in _config.Services)
            {
                var healthyInstances = new List<string>();

                foreach (var instance in service.Value.Instances)
                {
                    
                    bool isHealthy = true;

                    // When you add /health to your services, replace the line above with:
                    // bool isHealthy = await IsInstanceHealthy(instance);

                    if (isHealthy)
                    {
                        healthyInstances.Add(instance);
                    }
                }

                _instances[service.Key] = healthyInstances;

                _logger.LogInformation(
                    "Updated instances for {Service}: {Count} healthy instances.",
                    service.Key,
                    healthyInstances.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service instances.");
        }
    }

    private async Task<bool> IsInstanceHealthy(string baseUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}