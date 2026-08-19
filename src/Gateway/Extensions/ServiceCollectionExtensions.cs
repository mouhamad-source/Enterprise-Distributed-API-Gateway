using Gateway.Configuration;
using Gateway.HealthChecks;
using Gateway.Startup;

namespace Gateway.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProductionReadiness(this IServiceCollection services)
    {
        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();
        services.AddSingleton<IFeatureFlagProvider, ConfigurationFeatureFlagProvider>();
        services.AddSingleton<StartupValidator>();
        services.AddHostedService<ResourceMonitor>();

        return services;
    }
}