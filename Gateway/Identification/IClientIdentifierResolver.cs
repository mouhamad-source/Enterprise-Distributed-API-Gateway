using Microsoft.AspNetCore.Http;

namespace Gateway.Identification;

public interface IClientIdentifierResolver
{
    ClientIdentifier? Resolve(HttpContext context);
}