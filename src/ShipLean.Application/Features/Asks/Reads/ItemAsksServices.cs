using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Reads;

public sealed class ItemAsksServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IItemAsks, ItemAsks>();
}
