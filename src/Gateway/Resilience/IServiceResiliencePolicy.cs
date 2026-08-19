namespace Gateway.Resilience;

public interface IServiceResiliencePolicy
{
    Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> operation,
        CancellationToken cancellationToken = default);
}