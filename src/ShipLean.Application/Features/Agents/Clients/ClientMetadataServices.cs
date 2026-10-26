using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Http;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Clients;

public sealed class ClientMetadataServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(ClientMetadataOptions.Default);
        services.AddSingleton<IHostResolver, DnsHostResolver>();
        services.AddSingleton<IAddressCheck, PublicAddressCheck>();
        services.AddSingleton<PinnedConnector>();
        services.AddHttpClient(ClientMetadataOptions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false,
                ConnectCallback = sp.GetRequiredService<PinnedConnector>().ConnectAsync,
            });
        services.AddSingleton<IClientMetadataDocuments, ClientMetadataDocuments>();
    }
}
