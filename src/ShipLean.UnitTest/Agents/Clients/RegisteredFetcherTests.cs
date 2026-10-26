using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Agents.Clients;
using ShipLean.Application.Http;
using ShipLean.Contracts.Agents;

namespace ShipLean.UnitTest.Agents.Clients;

// The fetcher exactly as production registers it (ClientMetadataServices), with only the DNS answer swapped. If the
// registered HttpClient loses its ConnectCallback guard, these fail.
public sealed class RegisteredFetcherTests
{
    private sealed class FixedResolver(string address) : IHostResolver
    {
        public int Lookups { get; private set; }

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Parse(address)]);
        }
    }

    [TestCase("127.0.0.1")]
    [TestCase("169.254.169.254")]
    [TestCase("10.0.0.1")]
    [TestCase("::1")]
    public async Task Fetch_WhenHostResolvesNonPublic_ShouldRefuseWithoutConnecting(string address)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var resolver = new FixedResolver(address);
        var services = new ServiceCollection();
        new ClientMetadataServices().AddServices(services, new ConfigurationBuilder().Build());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IHostResolver>(resolver);
        await using var provider = (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);

        var document = await provider.GetRequiredService<IClientMetadataDocuments>()
            .FetchAsync($"https://client.example.test:{port}/oauth/metadata.json");

        document.ShouldBeNull();
        resolver.Lookups.ShouldBe(1);
        listener.Pending().ShouldBeFalse();
    }
}
