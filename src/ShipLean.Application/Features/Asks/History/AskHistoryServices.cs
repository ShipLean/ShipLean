using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.History;

public sealed class AskHistoryServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IAskHistory, AskHistory>();
}
