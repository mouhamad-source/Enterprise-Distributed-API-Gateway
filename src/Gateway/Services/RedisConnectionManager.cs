using Gateway.Configuration;
using StackExchange.Redis;

namespace Gateway.Services;

public class RedisConnectionManager : IDisposable
{
    private readonly ILogger<RedisConnectionManager> _logger;
    private readonly ConnectionMultiplexer _connection;
    private bool _disposed;

    public RedisConnectionManager(IConfiguration configuration, ILogger<RedisConnectionManager> logger)
    {
        _logger = logger;
        var config = configuration.GetSection("Redis").Get<RedisConfig>();
        var connectionString = config?.ConnectionString ?? "localhost:6379";
        _logger.LogInformation("Connecting to Redis at {ConnectionString}", connectionString);
        _connection = ConnectionMultiplexer.Connect(connectionString);
        _logger.LogInformation("Redis connection established.");
    }

    public IDatabase GetDatabase(int db = 0) => _connection.GetDatabase(db);

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;

        }
    }
}