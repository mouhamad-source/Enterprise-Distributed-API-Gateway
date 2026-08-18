using Gateway.Authentication;
using Gateway.Configuration;
using Gateway.Identification;
using Gateway.Interface.RateLimiting;
using Gateway.Middleware;
using Gateway.RateLimiting;
using Gateway.Resilience;
using Gateway.ServiceDiscovery;
using Gateway.Services;
using Gateway.Observability;
using Serilog;
using OpenTelemetry.Instrumentation.StackExchangeRedis;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Application", "Gateway")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();


builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Gateway"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;

                options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/metrics") &&
                                        !ctx.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation()
            .AddRedisInstrumentation()
            .AddJaegerExporter(options =>
            {
                options.AgentHost = builder.Configuration["OpenTelemetry:Jaeger:AgentHost"] ?? "localhost";
                options.AgentPort = int.Parse(builder.Configuration["OpenTelemetry:Jaeger:AgentPort"] ?? "6831");
            })
            .AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter(options =>
            {
                options.ScrapeEndpointPath = "/metrics";
            });
    });

var routesSection = builder.Configuration.GetSection("Routes");
Console.WriteLine($"Routes section exists: {routesSection.Exists()}");
Console.WriteLine($"Routes value: {routesSection.GetValue<string>("/users")}");


builder.Services.AddHttpClient("GatewayClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("HealthCheckClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(2);
});


builder.Services.AddSingleton<RedisConnectionManager>();


builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddSingleton<JwtTokenValidator>();


builder.Services.AddSingleton<IRateLimiter, RateLimiterService>();


builder.Services.AddSingleton<IServiceResiliencePolicy, PollyResiliencePolicy>();


builder.Services.Configure<ServiceRegistryConfig>(
    builder.Configuration);


builder.Services.AddSingleton<IServiceRegistry, InMemoryServiceRegistry>();


builder.Services.AddSingleton<ILoadBalancer, RoundRobinLoadBalancer>();


builder.Services.AddSingleton<IReverseProxy, DefaultReverseProxy>();


builder.Services.AddSingleton<IPResolver>();
builder.Services.AddSingleton<ApiKeyResolver>();
builder.Services.AddSingleton<IClientIdentifierResolver>(sp =>
{
    var resolvers = new IClientIdentifierResolver[]
    {
        sp.GetRequiredService<ApiKeyResolver>(),
        sp.GetRequiredService<IPResolver>()
    };
    return new CompositeResolver(resolvers);
});


builder.Services.AddSingleton<MetricsRegistry>();
builder.Services.AddControllers();
builder.Services.AddLogging();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.UseMiddleware<CorrelationIdMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var registry = scope.ServiceProvider.GetRequiredService<IServiceRegistry>();
    await registry.StartAsync();
}

app.UseMiddleware<AuthenticationMiddleware>();
app.UseMiddleware<GatewayMiddleware>();
app.MapControllers();
app.Run();