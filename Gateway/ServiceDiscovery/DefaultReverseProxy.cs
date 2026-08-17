using Microsoft.AspNetCore.Http;

namespace Gateway.ServiceDiscovery;

public class DefaultReverseProxy : IReverseProxy
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DefaultReverseProxy> _logger;

    public DefaultReverseProxy(IHttpClientFactory httpClientFactory, ILogger<DefaultReverseProxy> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HttpResponseMessage> ForwardAsync(
        HttpContext context,
        string instanceBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("GatewayClient");
        var targetUri = new Uri(new Uri(instanceBaseUrl), context.Request.Path + context.Request.QueryString);

        var proxyRequest = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            targetUri);

        
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                continue;
            proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

      
        if (context.Request.ContentLength > 0)
        {
            context.Request.Body.Position = 0;
            var streamContent = new StreamContent(context.Request.Body);
            if (context.Request.Headers.ContainsKey("Content-Type"))
            {
                streamContent.Headers.TryAddWithoutValidation(
                    "Content-Type",
                    context.Request.Headers["Content-Type"].ToString());
            }
            proxyRequest.Content = streamContent;
        }

        return await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
}