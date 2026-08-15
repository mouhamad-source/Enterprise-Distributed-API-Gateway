using Gateway.Identification;

namespace Gateway.Interface.RateLimiting;

public interface IRateLimiter
{
    bool IsRequestAllowed(ClientIdentifier clientId, out int currentCount);
}