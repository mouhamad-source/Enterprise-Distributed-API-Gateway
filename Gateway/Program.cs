using Gateway.Authentication;
using Gateway.Configuration;
using Gateway.Identification;
using Gateway.Interface.RateLimiting;
using Gateway.Middleware;
using Gateway.RateLimiting;
using Gateway.Resilience;
using Gateway.ServiceDiscovery;
using Gateway.Services;

var builder = WebApplication.CreateBuilder(args);


var servicesSection = builder.Configuration.GetSection("Services");
Console.WriteLine($"Services section exists: {servicesSection.Exists()}");
Console.WriteLine($"Services section value: {servicesSection.GetValue<string>("UserService:Instances:0")}");

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

builder.Services.AddLogging();

var app = builder.Build();

app.UseHttpsRedirection();


using (var scope = app.Services.CreateScope())
{
    var registry = scope.ServiceProvider.GetRequiredService<IServiceRegistry>();
    await registry.StartAsync();
}

app.UseMiddleware<AuthenticationMiddleware>();
app.UseMiddleware<GatewayMiddleware>();

app.Run();