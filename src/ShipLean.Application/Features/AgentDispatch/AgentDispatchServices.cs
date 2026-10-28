using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

public sealed class AgentDispatchServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AgentSessionStarter>();
        services.AddSingleton<AgentTrigger>();
        services.AddSingleton<AgentSessionEndpoints>();
        services.AddHostedService<AgentDispatchSubscriber>();

        // Webhooks out. The client never follows a redirect (a 3xx is a failed delivery) and gives up after Linear's 5 s.
        services.AddHttpClient(AgentDeliverer.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<AgentSessionNotifier>();
        services.AddSingleton<AgentDeliverer>();
        services.AddHostedService<AgentDeliveryService>();
        services.AddSingleton<AgentPrompter>();
        services.AddSingleton<IAgentPrompts>(sp => sp.GetRequiredService<AgentPrompter>());
        services.AddSingleton<AgentStaleSweeper>();
        services.AddHostedService<AgentStaleService>();
        services.AddScoped<IAgentReports, AgentReports>();
    }
}
