using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// IUsageEvents is a factory over the one concrete queue, so the dispatcher drains what Track writes. Register is public so a test
// fixture can opt back into the real queue over the host's RecordingUsageEvents (it re-adds only the IUsageEvents line, and the last wins).
public sealed class UsageServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<UsageQueue>();
        services.AddSingleton<IUsageScope, UsageScope>();
        services.AddHostedService<UsageDispatcher>();
        Register(services);
    }

    public static void Register(IServiceCollection services)
        => services.AddSingleton<IUsageEvents>(sp => sp.GetRequiredService<UsageQueue>());
}
