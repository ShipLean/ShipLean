using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Screens;

namespace ShipLean.Application.Features.Backlog.NeedsYou;

public sealed class NeedsYouServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<INeedsYouCounts, NeedsYouCounts>();
}
