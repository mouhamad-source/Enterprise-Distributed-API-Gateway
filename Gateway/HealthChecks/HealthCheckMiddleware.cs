using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Gateway.HealthChecks;

public class HealthCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHealthCheckService _healthCheckService;

    public HealthCheckMiddleware(RequestDelegate next, IHealthCheckService healthCheckService)
    {
        _next = next;
        _healthCheckService = healthCheckService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            var result = await _healthCheckService.CheckAsync();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = result.Status == "Healthy" ? 200 : 503;
            await context.Response.WriteAsync(JsonSerializer.Serialize(result));
            return;
        }
        await _next(context);
    }
}