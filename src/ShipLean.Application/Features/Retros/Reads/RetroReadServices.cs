using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Reads;

public sealed class RetroReadServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IRetroCards, RetroCards>();
}
