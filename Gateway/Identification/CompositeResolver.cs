using Microsoft.AspNetCore.Http;

namespace Gateway.Identification;

public class CompositeResolver : IClientIdentifierResolver
{
    private readonly IEnumerable<IClientIdentifierResolver> _resolvers;

    public CompositeResolver(IEnumerable<IClientIdentifierResolver> resolvers)
    {
       
        _resolvers = resolvers.OrderBy(r => GetPriority(r));
    }

    private static int GetPriority(IClientIdentifierResolver resolver)
    {
        return resolver switch
        {
            JwtResolver => 1,
            ApiKeyResolver => 2,
            IPResolver => 3,
            _ => 99
        };
    }

    public ClientIdentifier? Resolve(HttpContext context)
    {
        foreach (var resolver in _resolvers)
        {
            var id = resolver.Resolve(context);
            if (id != null)
                return id;
        }
        return null;
    }
}