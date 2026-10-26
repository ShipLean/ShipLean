using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.OpenGraph;

// ILinkCardSource has many implementations; this module adds the Open Graph one (Order 100, after the provider sources).
public sealed class OpenGraphServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ILinkCardSource, OpenGraphSource>();
}
