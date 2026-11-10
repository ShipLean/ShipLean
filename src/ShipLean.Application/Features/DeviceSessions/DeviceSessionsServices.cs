using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.DeviceSessions;

namespace ShipLean.Application.Features.DeviceSessions;

public sealed class DeviceSessionsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IDeviceSessions, DeviceSessions>();
}
