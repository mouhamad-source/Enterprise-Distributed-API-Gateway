using System.Diagnostics.Metrics;

namespace Gateway.Observability;

public class MetricsRegistry
{
    private readonly Meter _meter;

    public Counter<int> RequestCounter { get; }
    public Histogram<double> RequestDuration { get; }
    public Counter<int> RateLimitRejectedCounter { get; }
    public Counter<int> AuthenticationFailureCounter { get; }
    public Counter<int> CircuitBreakerOpenCounter { get; }
    public Counter<int> RetryCounter { get; }
    public Counter<int> ServiceUnavailableCounter { get; }
    public MetricsRegistry(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("Gateway.Metrics");

        RequestCounter = _meter.CreateCounter<int>("gateway.requests.total", 
            description: "Total number of requests");
        RequestDuration = _meter.CreateHistogram<double>("gateway.requests.duration", 
            unit: "ms", description: "Request duration in milliseconds");
        RateLimitRejectedCounter = _meter.CreateCounter<int>("gateway.rate_limit.rejected", 
            description: "Rate limit rejected requests");
        AuthenticationFailureCounter = _meter.CreateCounter<int>("gateway.authentication.failures", 
            description: "Authentication failures");
        CircuitBreakerOpenCounter = _meter.CreateCounter<int>("gateway.circuit_breaker.open", 
            description: "Circuit breaker open events");
        RetryCounter = _meter.CreateCounter<int>("gateway.retry.attempts", 
            description: "Retry attempts");
        ServiceUnavailableCounter = _meter.CreateCounter<int>("gateway.service_unavailable", 
            description: "Service unavailable (503) responses");
    }
}