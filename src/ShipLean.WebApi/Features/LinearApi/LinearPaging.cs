using HotChocolate;
using HotChocolate.Resolvers;

namespace ShipLean.WebApi.Features.LinearApi;

// Linear's connections: nodes and pageInfo. Paging is forward only: `first` (default 50, at most 250) and `after`, whose
// cursor is the id of the last node. `last` and `before` are accepted and ignored.
internal static class LinearPaging
{
    private const int DefaultFirst = 50;
    private const int MaxFirst = 250;

    public static int First(IResolverContext context)
        => Math.Clamp(context.ArgumentValue<int?>("first") ?? DefaultFirst, 1, MaxFirst);

    public static Guid? After(IResolverContext context)
    {
        if (context.ArgumentValue<string?>("after") is not { Length: > 0 } cursor)
        {
            return null;
        }
        return Guid.TryParse(cursor, out var id) ? id : throw LinearErrors.Invalid("The after cursor is not one this endpoint issued.");
    }

    public static IReadOnlyDictionary<string, object?> Connection(IReadOnlyList<IReadOnlyDictionary<string, object?>> nodes, bool hasNextPage) => new Dictionary<string, object?>
    {
        ["nodes"] = nodes,
        ["pageInfo"] = new Dictionary<string, object?>
        {
            ["hasNextPage"] = hasNextPage,
            ["hasPreviousPage"] = false,
            ["startCursor"] = nodes.Count == 0 ? null : nodes[0]["id"],
            ["endCursor"] = nodes.Count == 0 ? null : nodes[^1]["id"],
        },
    };
}
