namespace ShipLean.WebApi.Features.LinearApi;

// /linear/graphql lets an agent in with a connection's callback token and nothing else: the policy accepts only the token
// scheme, so the cookie and a proxy identity are never consulted there, and the fallback policy (a signed-in person) does not
// apply because the endpoint carries a policy of its own. The proxy is told to skip the path (ProxySignInPaths).
public static class LinearApiPolicy
{
    public const string Name = "linear";

    public const string Scheme = "linear-token";

    public const string Path = "/linear/graphql";

    public const string SchemaName = "linear";
}
