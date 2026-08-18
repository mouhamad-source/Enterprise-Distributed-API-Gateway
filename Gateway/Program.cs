using Gateway.Authentication;
using Gateway.Configuration;
using Gateway.HealthChecks;
using Gateway.Identification;
using Gateway.Interface.RateLimiting;
using Gateway.Middleware;
using Gateway.RateLimiting;
using Gateway.Resilience;
using Gateway.ServiceDiscovery;
using Gateway.Services;
using Gateway.Observability;
using Gateway.Startup;
using Gateway.Extensions;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Instrumentation.StackExchangeRedis;

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
                                        !ctx.Request.Path.StartsWithSegments("/health") &&
                                        !ctx.Request.Path.StartsWithSegments("/ready") &&
                                        !ctx.Request.Path.StartsWithSegments("/ops");
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


Console.WriteLine("=== Gateway Startup Configuration ===");

var routesSection = builder.Configuration.GetSection("Routes");
Console.WriteLine($"Routes section exists: {routesSection.Exists()}");
Console.WriteLine($"Routes value: {routesSection.GetValue<string>("/users")}");

var servicesSection = builder.Configuration.GetSection("Services");
Console.WriteLine($"Services section exists: {servicesSection.Exists()}");
Console.WriteLine($"Services value: {servicesSection.GetValue<string>("UserService:Instances:0")}");

var redisSection = builder.Configuration.GetSection("Redis");
Console.WriteLine($"Redis section exists: {redisSection.Exists()}");
Console.WriteLine($"Redis connection: {redisSection.GetValue<string>("ConnectionString")}");

Console.WriteLine("=======================================");


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


builder.Services.Configure<ServiceRegistryConfig>(builder.Configuration);
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
// builder.Services.AddScoped<CorrelationIdMiddleware>();


builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();


builder.Services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();


builder.Services.AddSingleton<IFeatureFlagProvider, ConfigurationFeatureFlagProvider>();


builder.Services.AddSingleton<StartupValidator>();
builder.Services.AddHostedService<StartupValidator>(sp => sp.GetRequiredService<StartupValidator>());


builder.Services.AddHostedService<ResourceMonitor>();


builder.Configuration.AddJsonFile("featureflags.json", optional: true, reloadOnChange: true);


builder.Services.AddControllers();


builder.Services.AddLogging();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();


app.UseCors("AllowAll");


app.UseHttpsRedirection();


app.UseOpenTelemetryPrometheusScrapingEndpoint();


var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("🛑 Shutting down gracefully...");
    Console.WriteLine("   Completing active requests...");

});

lifetime.ApplicationStopped.Register(() =>
{
    Console.WriteLine("✅ Shutdown complete.");
    Log.CloseAndFlush();
});


app.UseMiddleware<CorrelationIdMiddleware>();


using (var scope = app.Services.CreateScope())
{
    var registry = scope.ServiceProvider.GetRequiredService<IServiceRegistry>();
    await registry.StartAsync();
}


app.UseMiddleware<HealthCheckMiddleware>();   // /health
app.UseMiddleware<ReadinessMiddleware>();    // /ready


app.UseMiddleware<AuthenticationMiddleware>();
app.UseMiddleware<GatewayMiddleware>();


app.MapControllers();


Console.WriteLine("🚀 Gateway starting...");
Console.WriteLine($"   Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"   Listen on: http://localhost:5000");
Console.WriteLine($"   Health: http://localhost:5000/health");
Console.WriteLine($"   Readiness: http://localhost:5000/ready");
Console.WriteLine($"   Ops Dashboard: http://localhost:5000/ops");
Console.WriteLine($"   Metrics: http://localhost:5000/metrics");
Console.WriteLine("   Press Ctrl+C to stop");

app.Run();