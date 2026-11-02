using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Themes;

public sealed class RetroThemeReaderServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IRetroThemeReader, RetroThemeReader>();
}
