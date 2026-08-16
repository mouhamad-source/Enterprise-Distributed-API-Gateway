using Gateway.Identification;
using Gateway.Authentication; 
namespace Gateway.Interface.RateLimiting;

public interface IRateLimiter
{
    bool IsRequestAllowed(ClientIdentifier clientId, UserContext? userContext ,out int currentCount);
}