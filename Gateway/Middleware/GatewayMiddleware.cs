using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Gateway.Configuration;
using Gateway.RateLimiting;
using Gateway.Interface.RateLimiting;
using StackExchange.Redis;
using Gateway.Identification;
namespace Gateway.Middleware;

public class GatewayMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayMiddleware> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IRateLimiter _rateLimiter;
    private readonly IClientIdentifierResolver _identifierResolver; 
    public GatewayMiddleware(
        RequestDelegate next,
        ILogger<GatewayMiddleware> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IRateLimiter rateLimiter, 
        IClientIdentifierResolver identifierResolver
        )
    {
        _next = next;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _rateLimiter = rateLimiter;
        _identifierResolver = identifierResolver;
    }

    public async Task InvokeAsync(HttpContext context)
    {

        var clientId = _identifierResolver.Resolve(context); 
        if (clientId == null )
        {
            _logger.LogWarning("Unable to identify client, rejecting request.");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Client identification required.");
            return;
        }
        try
        {
            var allowed = _rateLimiter.IsRequestAllowed(clientId, out var currentCount);
            if (!allowed)
            {
                _logger.LogWarning("Rate limit exceeded for {ClientType}:{ClientValue}. Count: {Count}",
                    clientId.Type, clientId.Value, currentCount);
                context.Response.StatusCode = 429;
                context.Response.Headers["Retry-After"] = "60";
                await context.Response.WriteAsync("429 Too Many Requests - Rate limit exceeded.");
                return;
            }
            _logger.LogInformation("Request allowed for {ClientType}:{ClientValue}. Count: {Count}",
                clientId.Type, clientId.Value, currentCount);
        }
        catch (Exception ex) when (ex is RedisConnectionException || ex is TimeoutException)
        {
            
            _logger.LogError(ex, "Rate limiter unavailable (Redis down). Rejecting request.");
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("503 Service Unavailable - Rate limiter unavailable.");
            return;
        }
        var requestPath = context.Request.Path.Value ?? "/";
        var targetBaseUrl = GetTargetUrl(requestPath);

        if (string.IsNullOrEmpty(targetBaseUrl))
        {
            _logger.LogWarning("No route found for path: {Path}", requestPath);
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await context.Response.WriteAsync("No route matches this path.");
            return;
        }

        _logger.LogInformation("Forwarding request: {Method} {Path} -> {Target}",
            context.Request.Method, requestPath, targetBaseUrl);

        try
        {

            var client = _httpClientFactory.CreateClient("GatewayClient");
            client.Timeout = TimeSpan.FromSeconds(2); // المهلة 2 ثانية

            var targetUri = new Uri(new Uri(targetBaseUrl), requestPath);
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
                using var stream = new MemoryStream();
                await context.Request.Body.CopyToAsync(stream);
                stream.Position = 0;
                proxyRequest.Content = new StreamContent(stream);

                if (context.Request.Headers.ContainsKey("Content-Type"))
                {
                    proxyRequest.Content.Headers.TryAddWithoutValidation(
                        "Content-Type",
                        context.Request.Headers["Content-Type"].ToString());
                }
            }


            using var proxyResponse = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);


            context.Response.StatusCode = (int)proxyResponse.StatusCode;


            foreach (var header in proxyResponse.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in proxyResponse.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }


            if (proxyResponse.Content is not null)
            {
                await proxyResponse.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            }

            _logger.LogInformation("Forwarded response with status: {StatusCode}", proxyResponse.StatusCode);
        }
        catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError("Request timed out after 2 seconds.");
            context.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
            await context.Response.WriteAsync("Gateway Timeout - upstream service did not respond.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error forwarding request to upstream service.");
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            await context.Response.WriteAsync("Bad Gateway - upstream service unavailable.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during forwarding.");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync("Internal Server Error.");
        }
    }

    private string? GetTargetUrl(string requestPath)
    {
        var routes = _configuration.GetSection("Routes").Get<Dictionary<string, string>>();
        if (routes is null) return null;


        foreach (var route in routes)
        {
            if (requestPath.StartsWith(route.Key, StringComparison.OrdinalIgnoreCase))
                return route.Value;
        }
        return null;
    }
}