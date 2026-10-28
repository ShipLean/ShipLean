using HotChocolate.Resolvers;

namespace ShipLean.WebApi.Features.LinearApi;

// A value in an object map that is read only when the query selects the field (a connection, a nested read). The field
// defaults middleware calls it with the field's resolver context, so it can read the field's arguments.
public delegate ValueTask<object?> LinearLazy(IResolverContext context);
