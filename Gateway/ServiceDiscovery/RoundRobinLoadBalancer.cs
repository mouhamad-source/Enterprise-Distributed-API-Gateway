using System.Collections.Concurrent; 

namespace Gateway.ServiceDiscovery; 

public class RoundRobinLoadBalancer : ILoadBalancer
{
    private readonly ConcurrentDictionary<string , int > _indices = new(); 

    public string? SelectInstance(IReadOnlyList<string> instance)
    {
        if(instance == null || instance.Count == 0 )
            return null; 

        var key  = "default"; 

        var index = _indices.AddOrUpdate(key , 0 , (_, old) => (old + 1 ) % instance.Count); 
        return instance[index];     
    }
}