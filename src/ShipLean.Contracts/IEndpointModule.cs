using Microsoft.AspNetCore.Routing;

namespace ShipLean.Contracts;

// A feature's HTTP surface. Program.cs finds every implementation in ShipLean.WebApi and
// ShipLean.Application by assembly scan and calls MapEndpoints once, so adding a feature
// never means editing Program.cs. Implementations need a public parameterless constructor.
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
