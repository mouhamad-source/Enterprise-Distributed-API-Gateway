namespace Gateway.ServiceDiscovery;

public interface IServiceRegistry
{


    Task<IReadOnlyList<string>> GetInstancesAsync(string serviceName);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);


}