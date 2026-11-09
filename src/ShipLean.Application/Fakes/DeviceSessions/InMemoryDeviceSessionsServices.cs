using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.DeviceSessions;

namespace ShipLean.Application.Fakes.DeviceSessions;

public sealed class InMemoryDeviceSessionsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: replaced by the Postgres device-session store; until then sessions live only as long as the process.
        services.AddSingleton<IDeviceSessions, InMemoryDeviceSessions>();
    }
}
