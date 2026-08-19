using Microsoft.AspNetCore.Http;

namespace Gateway.Identification;

public class IPResolver : IClientIdentifierResolver
{
    public ClientIdentifier? Resolve(HttpContext context)
    {
        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                ?? context.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrEmpty(ip) || ip == "::1")
            ip = "127.0.0.1";
        return new ClientIdentifier("IP", ip);
    }
}