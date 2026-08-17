namespace Gateway.ServiceDiscovery; 

public interface ILoadBalancer
{
    string? SelectInstance(IReadOnlyList<string> instances);
}