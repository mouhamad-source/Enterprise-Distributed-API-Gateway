using Microsoft.AspNetCore.Http; 

namespace Gateway.ServiceDiscovery; 


public interface IReverseProxy
{
    Task<HttpResponseMessage> ForwardAsync(
        HttpContext context,
        string instanceBaseUrl,
        CancellationToken cancellationToken = default);
}