using HotChocolate.Resolvers;
using ShipLean.Application.Features.LinearApi;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.LinearApi;

// What a resolver needs about the call: which connection is asking, the reads (every one scoped to that connection) and the
// base URL for the links it returns.
public sealed class LinearRequest(LinearCaller caller, LinearReads reads, string fallbackOrigin)
{
    private string? _baseUrl;

    public LinearCaller Caller => caller;

    public LinearReads Reads => reads;

    public static LinearRequest Of(IResolverContext context)
    {
        var request = context.Service<ICurrentRequest>();
        if (request.User is not { } user)
        {
            throw new InvalidOperationException("The Linear-compatible endpoint ran outside a request.");
        }
        return new LinearRequest(LinearCaller.Of(user), context.Service<LinearReads>(), request.Origin);
    }

    public async ValueTask<string> BaseUrlAsync(CancellationToken cancellationToken)
        => _baseUrl ??= await reads.BaseUrlAsync(fallbackOrigin, cancellationToken);
}
