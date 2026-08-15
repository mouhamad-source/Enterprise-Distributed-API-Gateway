using Microsoft.AspNetCore.Http;

namespace Gateway.Identification;

public class ApiKeyResolver : IClientIdentifierResolver
{
    public ClientIdentifier? Resolve(HttpContext context)
    {
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
            apiKey = context.Request.Query["api_key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
            return null;
        return new ClientIdentifier("ApiKey", apiKey);
    }
}