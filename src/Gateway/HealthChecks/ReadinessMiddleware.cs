using Microsoft.AspNetCore.Http;

namespace Gateway.HealthChecks;

public class ReadinessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHealthCheckService _healthCheckService;
    private bool _isReady = false;

    public ReadinessMiddleware(RequestDelegate next, IHealthCheckService healthCheckService)
    {
        _next = next;
        _healthCheckService = healthCheckService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/ready"))
        {
            if (!_isReady)
            {
                
                var result = await _healthCheckService.CheckAsync();
                _isReady = result.Status != "Unhealthy";
            }

            context.Response.StatusCode = _isReady ? 200 : 503;
            await context.Response.WriteAsync(_isReady ? "Ready" : "Not Ready");
            return;
        }
        await _next(context);
    }
}