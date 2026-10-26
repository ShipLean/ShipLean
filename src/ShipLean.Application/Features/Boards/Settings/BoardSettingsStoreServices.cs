using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Boards;

namespace ShipLean.Application.Features.Boards.Settings;

public sealed class BoardSettingsStoreServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBoardSettingsStore, BoardSettingsStore>();
}
