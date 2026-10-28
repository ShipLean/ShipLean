using HotChocolate.Resolvers;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using ShipLean.Application.Features.LinearApi;
using ShipLean.WebApi.Infrastructure;

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
        var http = context.Service<IHttpContextAccessor>().HttpContext ?? throw new InvalidOperationException("The Linear-compatible endpoint ran outside a request.");
        return new LinearRequest(
            LinearCaller.Of(http.User),
            context.Service<LinearReads>(),
            RequestOrigin.Of(http.Request, context.Service<IOptions<ForwardedHeadersOptions>>().Value));
    }

    public async ValueTask<string> BaseUrlAsync(CancellationToken cancellationToken)
        => _baseUrl ??= await reads.BaseUrlAsync(fallbackOrigin, cancellationToken);
}
