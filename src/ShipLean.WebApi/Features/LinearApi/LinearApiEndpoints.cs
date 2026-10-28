using HotChocolate.AspNetCore;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.LinearApi;

// POST /linear/graphql: the Linear-compatible GraphQL endpoint (Half 2 of the dispatch protocol). Outside /api, because
// /api never takes a bearer. The policy replaces the fallback policy and names the token scheme only; the Banana Cake Pop page is off.
public sealed class LinearApiEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGraphQL(LinearApiPolicy.Path, LinearApiPolicy.SchemaName)
            .WithOptions(options => options.Tool.Enable = false)
            .RequireAuthorization(LinearApiPolicy.Name);
    }
}
