using System.Diagnostics;
using Microsoft.AspNetCore.Http;


namespace Gateway.Observability;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
{
    var traceParent = context.Request.Headers["traceparent"].FirstOrDefault();
    ActivityContext? parentContext = null;
    if (!string.IsNullOrEmpty(traceParent) && 
        ActivityContext.TryParse(traceParent, null, out var parsedContext))
    {
        parentContext = parsedContext;
    }

    using var activity = DiagnosticConfig.Source.StartActivity(
        "Gateway Request", 
        ActivityKind.Server, 
        parentContext ?? default);

    if (activity != null)
    {
        activity.AddTag("http.method", context.Request.Method);
        activity.AddTag("http.url", context.Request.Path + context.Request.QueryString);
        activity.AddTag("http.client_ip", context.Connection.RemoteIpAddress?.ToString());
    }

    // ✅ إضافة TraceId إلى الـ Response Headers قبل بدء الاستجابة
    if (activity != null && !context.Response.HasStarted)
    {
        context.Response.Headers["x-trace-id"] = activity.TraceId.ToString();
    }

    using (_logger.BeginScope(new Dictionary<string, object>
    {
        ["TraceId"] = activity?.TraceId.ToString() ?? "unknown",
        ["SpanId"] = activity?.SpanId.ToString() ?? "unknown",
        ["RequestId"] = context.TraceIdentifier
    }))
    {
        await _next(context);
    }
}


}


public static class DiagnosticConfig
{
    public static readonly ActivitySource Source = new("Gateway");
}