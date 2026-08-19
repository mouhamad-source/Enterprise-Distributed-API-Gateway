namespace Gateway.Configuration;

public interface ISecretProvider
{
    string? GetSecret(string key);
}