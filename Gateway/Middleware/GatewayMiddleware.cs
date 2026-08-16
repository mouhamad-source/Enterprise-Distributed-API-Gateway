using System.Net;
using Gateway.Authentication;
using Gateway.Identification;
using Gateway.Interface.RateLimiting;
using Gateway.RateLimiting;
using Gateway.Resilience;
using Polly;
using Polly.CircuitBreaker;
using StackExchange.Redis;

namespace Gateway.Middleware;

public class GatewayMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayMiddleware> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IRateLimiter _rateLimiter;
    private readonly IClientIdentifierResolver _identifierResolver;
    private readonly IServiceResiliencePolicy _resiliencePolicy;

    public GatewayMiddleware(
        RequestDelegate next,
        ILogger<GatewayMiddleware> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IRateLimiter rateLimiter,
        IClientIdentifierResolver identifierResolver,
        IServiceResiliencePolicy resiliencePolicy)
    {
        _next = next;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _rateLimiter = rateLimiter;
        _identifierResolver = identifierResolver;
        _resiliencePolicy = resiliencePolicy;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();


        var clientId = _identifierResolver.Resolve(context);
        if (clientId == null)
        {
            _logger.LogWarning("Unable to identify client, rejecting request.");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsync("Client identification required.");
            return;
        }

        var userContext = context.Items["UserContext"] as UserContext;


        try
        {
            var allowed = _rateLimiter.IsRequestAllowed(clientId, userContext, out var currentCount);
            if (!allowed)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for {ClientType}:{ClientValue}. Count: {Count}, User: {UserId}",
                    clientId.Type,
                    clientId.Value,
                    currentCount,
                    userContext?.UserId ?? "anonymous");

                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers["Retry-After"] = "60";
                await context.Response.WriteAsync("429 Too Many Requests - Rate limit exceeded.");
                return;
            }

            _logger.LogInformation(
                "Request allowed for {ClientType}:{ClientValue}. Count: {Count}, User: {UserId}",
                clientId.Type,
                clientId.Value,
                currentCount,
                userContext?.UserId ?? "anonymous");
        }
        catch (Exception ex) when (ex is RedisConnectionException || ex is TimeoutException)
        {
            _logger.LogError(ex, "Rate limiter unavailable (Redis down). Rejecting request.");
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await context.Response.WriteAsync("503 Service Unavailable - Rate limiter temporarily unavailable.");
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


        try
        {
            var client = _httpClientFactory.CreateClient("GatewayClient");


            var targetUri = new Uri(new Uri(targetBaseUrl), requestPath);


            var response = await _resiliencePolicy.ExecuteAsync(
                async (cancellationToken) =>
                {

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


                    return await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead
, cancellationToken);
                },
                context.RequestAborted);


            context.Response.StatusCode = (int)response.StatusCode;


            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }


            if (response.Content != null)
            {
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            }

            _logger.LogInformation(
                "Request forwarded to {Target} with status {StatusCode}",
                targetBaseUrl,
                response.StatusCode);
        }
        catch (BrokenCircuitException)
        {

            _logger.LogWarning("Circuit breaker is OPEN for service {Target}.", targetBaseUrl);
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await context.Response.WriteAsync(
                "503 Service Unavailable - Service temporarily blocked (circuit open).");
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {

            _logger.LogWarning("Request timed out after policy timeout for {Target}.", targetBaseUrl);
            context.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
            await context.Response.WriteAsync("504 Gateway Timeout - Upstream service took too long.");
        }
        catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {

            _logger.LogWarning("Request cancelled due to timeout for {Target}.", targetBaseUrl);
            context.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
            await context.Response.WriteAsync("504 Gateway Timeout - Upstream service took too long.");
        }
        catch (HttpRequestException ex)
        {

            _logger.LogError(ex, "Error forwarding request to {Target}.", targetBaseUrl);
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            await context.Response.WriteAsync("502 Bad Gateway - Upstream service unavailable.");
        }
        catch (Exception ex)
        {

            _logger.LogError(ex, "Unexpected error forwarding request to {Target}.", targetBaseUrl);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync("500 Internal Server Error - An unexpected error occurred.");
        }
    }

    private string? GetTargetUrl(string requestPath)
    {
        var routes = _configuration.GetSection("Routes").Get<Dictionary<string, string>>();

        if (routes == null)
            return null;


        foreach (var route in routes)
        {
            if (requestPath.StartsWith(route.Key, StringComparison.OrdinalIgnoreCase))
                return route.Value;
        }

        return null;
    }
}