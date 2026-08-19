namespace Gateway.Configuration;

public class EnvironmentSecretProvider : ISecretProvider
{
    public string? GetSecret(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }
}