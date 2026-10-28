using HotChocolate.Resolvers;

namespace ShipLean.WebApi.Features.LinearApi;

// The Mutation fields. SHIP-187 (C2) fills these in; until then each is a clear GraphQL error rather than a default payload
// that would claim success. The schema already carries them, so a document that selects them validates.
internal static class LinearMutations
{
    public static ValueTask<object?> AgentActivityCreate(IResolverContext context) => throw LinearErrors.NotAvailable("agentActivityCreate");

    public static ValueTask<object?> AgentSessionUpdate(IResolverContext context) => throw LinearErrors.NotAvailable("agentSessionUpdate");

    public static ValueTask<object?> AgentSessionUpdateExternalUrl(IResolverContext context) => throw LinearErrors.NotAvailable("agentSessionUpdateExternalUrl");

    public static ValueTask<object?> CommentCreate(IResolverContext context) => throw LinearErrors.NotAvailable("commentCreate");
}
