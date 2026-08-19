using Microsoft.Extensions.Configuration;

namespace Gateway.Configuration;

public class ConfigurationFeatureFlagProvider : IFeatureFlagProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationFeatureFlagProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsEnabled(string feature)
    {
        return _configuration.GetValue<bool>($"FeatureFlags:{feature}", false);
    }

    public T? GetValue<T>(string feature)
    {
        return _configuration.GetValue<T>($"FeatureFlags:{feature}");
    }
}