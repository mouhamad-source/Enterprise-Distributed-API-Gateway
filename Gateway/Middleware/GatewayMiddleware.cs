using System.Diagnostics;
using System.Net;
using Gateway.Authentication;
using Gateway.Identification;
using Gateway.Interface.RateLimiting;
using Gateway.Observability;
using Gateway.RateLimiting;
using Gateway.Resilience;
using Gateway.ServiceDiscovery;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StackExchange.Redis;

namespace Gateway.Middleware;

public class GatewayMiddleware
{
    private readonly MetricsRegistry _metrics;
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRateLimiter _rateLimiter;
    private readonly IClientIdentifierResolver _identifierResolver;
    private readonly IServiceResiliencePolicy _resiliencePolicy;
    private readonly IServiceRegistry _serviceRegistry;
    private readonly ILoadBalancer _loadBalancer;
    private readonly IReverseProxy _reverseProxy;

    public GatewayMiddleware(
        RequestDelegate next,
        ILogger<GatewayMiddleware> logger,
        IConfiguration configuration,
        IRateLimiter rateLimiter,
        IClientIdentifierResolver identifierResolver,
        IServiceResiliencePolicy resiliencePolicy,
        IServiceRegistry serviceRegistry,
        ILoadBalancer loadBalancer,
        IReverseProxy reverseProxy,
        MetricsRegistry metrics)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
        _rateLimiter = rateLimiter;
        _identifierResolver = identifierResolver;
        _resiliencePolicy = resiliencePolicy;
        _serviceRegistry = serviceRegistry;
        _loadBalancer = loadBalancer;
        _reverseProxy = reverseProxy;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;
        var statusCode = 0;
        string? serviceName = null;
        string? selectedInstance = null;
        bool isRateLimited = false;
        bool isAuthFailure = false;
        bool isCircuitBreakerOpen = false;
        // bool isRetry = false;

        try
        {

            context.Request.EnableBuffering();

            var clientId = _identifierResolver.Resolve(context);
            if (clientId == null)
            {
                isAuthFailure = true;
                _logger.LogWarning(
                    "Unable to identify client, rejecting request. Path: {Path}, Method: {Method}",
                    requestPath, method);
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                statusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.WriteAsync("Client identification required.");
                return;
            }


            var userContext = context.Items["UserContext"] as UserContext;


            try
            {
                var allowed = _rateLimiter.IsRequestAllowed(clientId, userContext, out var currentCount);
                if (!allowed)
                {
                    isRateLimited = true;
                    _logger.LogWarning(
                        "Rate limit exceeded for {ClientType}:{ClientValue}. Count: {Count}, User: {UserId}, Path: {Path}, Method: {Method}",
                        clientId.Type,
                        clientId.Value,
                        currentCount,
                        userContext?.UserId ?? "anonymous",
                        requestPath,
                        method);

                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Headers["Retry-After"] = "60";
                    statusCode = (int)HttpStatusCode.TooManyRequests;
                    await context.Response.WriteAsync("429 Too Many Requests - Rate limit exceeded.");
                    return;
                }

                _logger.LogDebug(
                    "Request allowed for {ClientType}:{ClientValue}. Count: {Count}, User: {UserId}",
                    clientId.Type,
                    clientId.Value,
                    currentCount,
                    userContext?.UserId ?? "anonymous");
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is TimeoutException)
            {
                _logger.LogError(ex,
                    "Rate limiter unavailable (Redis down). Rejecting request. Path: {Path}, Method: {Method}",
                    requestPath, method);
                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                await context.Response.WriteAsync("503 Service Unavailable - Rate limiter temporarily unavailable.");
                return;
            }


            serviceName = GetServiceName(requestPath);
            _logger.LogDebug("Resolved service name: {ServiceName} for path: {Path}", serviceName, requestPath);

            if (string.IsNullOrEmpty(serviceName))
            {
                _logger.LogWarning("No service found for path: {Path}, Method: {Method}", requestPath, method);
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                statusCode = (int)HttpStatusCode.NotFound;
                await context.Response.WriteAsync("No route matches this path.");
                return;
            }


            var instances = await _serviceRegistry.GetInstancesAsync(serviceName);
            if (instances == null || instances.Count == 0)
            {
                _logger.LogWarning("No available instances for service: {Service}, Path: {Path}", serviceName, requestPath);
                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                await context.Response.WriteAsync("503 Service Unavailable - No instances available.");
                return;
            }


            selectedInstance = _loadBalancer.SelectInstance(instances);
            if (string.IsNullOrEmpty(selectedInstance))
            {
                _logger.LogWarning("Failed to select instance for service: {Service}", serviceName);
                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                await context.Response.WriteAsync("503 Service Unavailable - Cannot select instance.");
                return;
            }

            _logger.LogDebug(
                "Selected instance {Instance} for service {Service} (path: {Path})",
                selectedInstance,
                serviceName,
                requestPath);


            try
            {
                var response = await _resiliencePolicy.ExecuteAsync(
                    async (cancellationToken) =>
                    {
                        return await _reverseProxy.ForwardAsync(
                            context,
                            selectedInstance,
                            cancellationToken);
                    },
                    context.RequestAborted);

                statusCode = (int)response.StatusCode;
                context.Response.StatusCode = statusCode;


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
                    "Request forwarded to {Instance} with status {StatusCode}, Path: {Path}, Method: {Method}",
                    selectedInstance,
                    response.StatusCode,
                    requestPath,
                    method);
            }
            catch (BrokenCircuitException)
            {
                isCircuitBreakerOpen = true;
                _logger.LogWarning(
                    "Circuit breaker is OPEN for service {Service} (instance: {Instance}). Path: {Path}, Method: {Method}",
                    serviceName,
                    selectedInstance,
                    requestPath,
                    method);
                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                await context.Response.WriteAsync(
                    "503 Service Unavailable - Service temporarily blocked (circuit open).");
            }
            catch (TimeoutRejectedException)
            {
                _logger.LogWarning(
                    "Request timed out after policy timeout for {Service} (instance: {Instance}). Path: {Path}, Method: {Method}",
                    serviceName,
                    selectedInstance,
                    requestPath,
                    method);
                context.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
                statusCode = (int)HttpStatusCode.GatewayTimeout;
                await context.Response.WriteAsync("504 Gateway Timeout - Upstream service took too long.");
            }
            catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Request cancelled due to timeout for {Service} (instance: {Instance}). Path: {Path}, Method: {Method}",
                    serviceName,
                    selectedInstance,
                    requestPath,
                    method);
                context.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
                statusCode = (int)HttpStatusCode.GatewayTimeout;
                await context.Response.WriteAsync("504 Gateway Timeout - Upstream service took too long.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "Error forwarding request to {Service} (instance: {Instance}). Path: {Path}, Method: {Method}",
                    serviceName,
                    selectedInstance,
                    requestPath,
                    method);
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                statusCode = (int)HttpStatusCode.BadGateway;
                await context.Response.WriteAsync("502 Bad Gateway - Upstream service unavailable.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error forwarding request to {Service} (instance: {Instance}). Path: {Path}, Method: {Method}",
                    serviceName,
                    selectedInstance,
                    requestPath,
                    method);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                statusCode = (int)HttpStatusCode.InternalServerError;
                await context.Response.WriteAsync("500 Internal Server Error - An unexpected error occurred.");
            }
        }
        finally
        {

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;


            _metrics.RequestDuration.Record(durationMs);


            _metrics.RequestCounter.Add(1,
                new KeyValuePair<string, object?>("method", method),
                new KeyValuePair<string, object?>("path", requestPath),
                new KeyValuePair<string, object?>("status_code", statusCode),
                new KeyValuePair<string, object?>("service", serviceName ?? "unknown"));


            if (isRateLimited)
            {
                _metrics.RateLimitRejectedCounter.Add(1);
            }
            if (isAuthFailure)
            {
                _metrics.AuthenticationFailureCounter.Add(1);
            }
            if (isCircuitBreakerOpen)
            {
                _metrics.CircuitBreakerOpenCounter.Add(1);
            }
            if (statusCode == (int)HttpStatusCode.ServiceUnavailable && !isCircuitBreakerOpen)
            {
                _metrics.ServiceUnavailableCounter.Add(1);
            }
            if (statusCode == (int)HttpStatusCode.GatewayTimeout || statusCode == (int)HttpStatusCode.BadGateway)
            {
                _metrics.RetryCounter.Add(1);
            }


            _logger.LogInformation(
                "Request completed: {Method} {Path} → {Service} ({Instance}) → {StatusCode} in {Duration:F2} ms, TraceId: {TraceId}",
                method,
                requestPath,
                serviceName ?? "unknown",
                selectedInstance ?? "none",
                statusCode,
                durationMs,
                Activity.Current?.TraceId.ToString() ?? "unknown");



        }
    }

    private string? GetServiceName(string requestPath)
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
