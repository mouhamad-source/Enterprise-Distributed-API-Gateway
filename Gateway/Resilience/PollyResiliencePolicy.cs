using Polly;
using Polly.Timeout;
using System.Net; 

namespace Gateway.Resilience; 


public class PollyResiliencePolicy : IServiceResiliencePolicy
{
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;
    private readonly ILogger<PollyResiliencePolicy> _logger;
    private readonly ResilienceConfig _config;

    public PollyResiliencePolicy(IConfiguration configuration, ILogger<PollyResiliencePolicy> logger)
    {
        _logger = logger;
        _config = configuration.GetSection("Resilience").Get<ResilienceConfig>() 
                ?? new ResilienceConfig();

        var timeoutPolicy = Policy
            .TimeoutAsync<HttpResponseMessage>(
                TimeSpan.FromSeconds(_config.TimeoutSeconds),
                TimeoutStrategy.Pessimistic,
                onTimeoutAsync: (context, timespan, task) =>
                {
                    _logger.LogWarning("Request timed out after {Timeout}s.", timespan.TotalSeconds);
                    return Task.CompletedTask;
                });
                

        var retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => IsTransientError(r.StatusCode))
            .Or<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: _config.RetryCount,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(
                    _config.BaseDelayMilliseconds * Math.Pow(2, attempt - 1)), // 100, 200, 400 ms
                onRetry: (outcome, timespan, attempt, context) =>
                {
                    _logger.LogWarning(
                        "Retry {Attempt} after {Delay}ms due to {Status}.",
                        attempt,
                        timespan.TotalMilliseconds,
                        outcome?.Result?.StatusCode ?? HttpStatusCode.InternalServerError);
                });
        var circuitBreakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => IsTransientError(r.StatusCode))
            .Or<HttpRequestException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: _config.ExceptionsAllowedBeforeBreaking,
                durationOfBreak: TimeSpan.FromSeconds(_config.BreakDurationSeconds),
                onBreak: (outcome, breakDelay) =>
                {
                    _logger.LogWarning(
                        "Circuit breaker OPEN for {BreakDelay}s due to {Status}.",
                        breakDelay.TotalSeconds,
                        outcome?.Result?.StatusCode ?? HttpStatusCode.InternalServerError);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker CLOSED (reset).");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker HALF-OPEN (testing).");
                });
        _policy = Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreakerPolicy);        
    }


    public async Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> operation,
        CancellationToken cancellationToken = default)
    {
        return await _policy.ExecuteAsync(operation, cancellationToken);
    }

     private static bool IsTransientError(HttpStatusCode statusCode)
    {
        return  statusCode == HttpStatusCode.RequestTimeout ||
                statusCode == HttpStatusCode.InternalServerError ||
                statusCode == HttpStatusCode.BadGateway ||
                statusCode == HttpStatusCode.ServiceUnavailable ||
                statusCode == HttpStatusCode.GatewayTimeout;
    }
}