using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Import;

namespace ShipLean.Application.Features.Import.Jira;

// The Jira client over the named HttpClient (JiraConnector is its primary handler, so addresses are checked when connecting).
public sealed class JiraClientServices : IServiceModule
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(JiraTransport.HttpClientName, client => client.Timeout = RequestTimeout)
            .ConfigurePrimaryHttpMessageHandler(sp => JiraConnector.CreateHandler(sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton<JiraTransport>();
        services.AddSingleton<IJiraClient, JiraClient>();
    }
}
