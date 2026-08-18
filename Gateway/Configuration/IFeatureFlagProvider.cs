namespace Gateway.Configuration;

public interface IFeatureFlagProvider
{
    bool IsEnabled(string feature);
    T? GetValue<T>(string feature);
}