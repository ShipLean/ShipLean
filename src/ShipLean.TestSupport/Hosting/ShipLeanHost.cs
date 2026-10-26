using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.Search.Index;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Http;
using ShipLean.Application.Storage;
using ShipLean.Application.Identity;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Search;
using ShipLean.TestSupport.Http;
using ShipLean.WebApi;
using ShipLean.WebApi.Realtime;

namespace ShipLean.TestSupport.Hosting;

// The real app (WebApi's Program and DI) on Kestrel at http://127.0.0.1:<free port>, against one database. A browser
// can reach it, and it serves the SPA bundle that `pnpm run build` emitted into src/ShipLean.WebApi/wwwroot. The only
// seam is configureTestServices, which runs after Program.cs has registered everything: that is where a test swaps a
// double.
public sealed class ShipLeanHost : IAsyncDisposable
{
    private const string SearchCutoffKey = "SHIPLEAN_SEARCH_CUTOFF";

    private readonly WebApplicationFactory<ApiMarker> _factory;

    private ShipLeanHost(WebApplicationFactory<ApiMarker> factory, string baseUrl)
    {
        _factory = factory;
        BaseUrl = baseUrl;
    }

    public string BaseUrl { get; }

    public IServiceProvider Services => _factory.Services;

    // Requests without a sign-in cookie act as Joel.
    public static ShipLeanHost Start(string connectionString, Action<IServiceCollection>? configureTestServices = null)
        => Start(connectionString, TestUser.Joel, configureTestServices);

    // user null: no test sign-in at all, so only the real cookie signs anyone in (the browser tests). Otherwise
    // TestAuthentication goes in first, so a fixture's ConfigureTestServices can still override it.
    // listenOnAllInterfaces: Kestrel also answers on the machine's other addresses, for a test that needs a peer that
    // is not loopback (BaseUrl still names 127.0.0.1).
    // redisConnection: SHIPLEAN_REDIS_CONNECTION, for hosts that act as instances of one deployment (RedisServer).
    // settings: more app configuration, applied last (a blob store connection, say).
    public static ShipLeanHost Start(
        string connectionString,
        TestUser? user,
        Action<IServiceCollection>? configureTestServices = null,
        bool listenOnAllInterfaces = false,
        string? redisConnection = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var factory = new WebApplicationFactory<ApiMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(RepoPaths.WebApiProject);
            // Port 0: the OS picks a free port. UseKestrel(0) alone leaves the app on the default :5000.
            builder.UseSetting(WebHostDefaults.ServerUrlsKey, listenOnAllInterfaces ? "http://0.0.0.0:0" : "http://127.0.0.1:0");
            // A setting, not app configuration: RealtimeServices reads it while registering, before the factory's
            // ConfigureAppConfiguration applies. Empty (in-process realtime) unless the test asks for Redis.
            builder.UseSetting(RealtimeServices.RedisConnectionKey, redisConnection ?? string.Empty);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DatabaseServices.ConnectionKey] = connectionString,
                // A developer's first-admin variables must not leak into a test database.
                [AdminBootstrap.EmailKey] = string.Empty,
                [AdminBootstrap.PasswordKey] = string.Empty,
                // Nor a developer's blob store: tests use the folder unless they pass a connection in settings.
                [BlobStoreServices.DbConnectionKey] = string.Empty,
                [BlobStoreServices.S3ConnectionKey] = string.Empty,
                // Nor a developer's webhook secrets: the endpoints stay off unless a test turns them on in settings.
                [WebhookSecrets.GithubKey] = string.Empty,
                [WebhookSecrets.GitlabKey] = string.Empty,
                // The production cut-off default is tuned to e5's cosines, which sit high; the FakeEmbedder's sit lower, so tests keep the old floor.
                [SearchCutoffKey] = "0.55",
            }));
            if (settings != null)
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            }
            // Before the fixture's own services, so a fixture can still swap in its own resolver. No test looks a name up on the real network.
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IHostResolver>(new NoNetworkHostResolver())));
            // Likewise the embedder: the fake is deterministic and needs no model. A fixture opts back into the real factory with EmbedderServices.Register.
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IEmbedder>(new FakeEmbedder())));
            // No background indexing: it would race the exact-count pass tests and sync rows builders arrange without a source. A service test adds SearchIndexHostedService back.
            builder.ConfigureTestServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(SearchIndexHostedService)).ToList())
                {
                    services.Remove(descriptor);
                }
            });
            if (user != null)
            {
                builder.ConfigureTestServices(services => TestAuthentication.Add(services, user));
            }
            if (configureTestServices != null)
            {
                builder.ConfigureTestServices(configureTestServices);
            }
        });
        factory.UseKestrel(0);
        factory.StartServer();
        var address = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new ShipLeanHost(factory, $"http://127.0.0.1:{new Uri(address).Port}");
    }

    // Whether `pnpm run build` has emitted the SPA into the WebApi's wwwroot.
    public static bool HasClientBundle() => File.Exists(Path.Combine(RepoPaths.WebApiProject, "wwwroot", "index.html"));

    // A test that calls a service straight from Host.Services (no request) acts as userId (null: nobody), as a request would.
    public void ActAs(Guid? userId)
    {
        if (Services.GetService<ActingCurrentUser>() is { } acting)
        {
            acting.Acting = userId;
        }
    }

    // An access token for an agent acting as user, with the given scopes (default: read), audience-bound to this host's
    // /mcp URL. It goes only through IOAuthClients, IAgentAuthorizations and IAgentTokenIssuer, so it works unchanged on
    // the in-memory fakes and on the Postgres stores. Start the host with user null to drive /mcp with it.
    public async Task<string> IssueAgentToken(TestUser user, IReadOnlySet<string>? scopes = null, string clientName = "Test Agent")
    {
        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var baseUrl = await ReadBaseUrlAsync(provider.GetRequiredService<NpgsqlDataSource>());
        var resource = AgentUrls.Resource(baseUrl, BaseUrl);

        var client = await provider.GetRequiredService<IOAuthClients>()
            .RegisterAsync(new OAuthClientRegistration(clientName, ["http://127.0.0.1:8123/callback"]));
        var authorizationId = await provider.GetRequiredService<IAgentAuthorizations>()
            .CreateAsync(user.Id ?? throw new ArgumentException("An agent token needs a signed-in user.", nameof(user)), client.ClientId, scopes ?? new HashSet<string> { "shiplean:read" });
        return (await provider.GetRequiredService<IAgentTokenIssuer>().IssueAsync(authorizationId, resource)).AccessToken;
    }

    // Read-only: the resource an agent token must name depends on instance_settings.base_url.
    private static async Task<string?> ReadBaseUrlAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<string>("SELECT base_url FROM instance_settings");
    }

    public HttpClient CreateClient() => new() { BaseAddress = new Uri(BaseUrl) };

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
