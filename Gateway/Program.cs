
using Gateway.Interface.RateLimiting;
using Gateway.Middleware;
using Gateway.RateLimiting;
using Gateway.Services;
using Gateway.Identification; 
using Gateway.Authentication; 

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient("GatewayClient", client =>
{

});

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddSingleton<JwtTokenValidator>();


builder.Services.AddSingleton<RedisConnectionManager>();
builder.Services.AddSingleton<IRateLimiter, RateLimiterService>();

// builder.Services.AddSingleton<IRateLimiter, MemoryRateLimiter>();
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

app.UseMiddleware<AuthenticationMiddleware>();

app.UseMiddleware<GatewayMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("Transfer-Encoding");
    await next();
});

app.Run();


