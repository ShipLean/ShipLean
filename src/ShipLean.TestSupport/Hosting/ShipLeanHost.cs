using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Application.Http;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Search;
using ShipLean.LocalEnvironment;
using ShipLean.LocalEnvironment.Hosting;
using ShipLean.TestSupport.Http;
using ShipLean.TestSupport.Search;

namespace ShipLean.TestSupport.Hosting;

// The real app (WebApi's Program and DI) on Kestrel at http://127.0.0.1:<free port>, against one database: an AppHost with
// the Test profile, plus the doubles every test host gets. A browser can reach it, and it serves the SPA bundle that
// `pnpm run build` emitted into src/ShipLean.WebApi/wwwroot. The only seam is configureTestServices, which runs after
// Program.cs has registered everything: that is where a test swaps a double.
public sealed class ShipLeanHost : IAsyncDisposable
{
    // The production cut-off default is tuned to e5's cosines, which sit high; the FakeEmbedder's sit lower, so tests keep the old floor.
    private const string SearchCutoffKey = "SHIPLEAN_SEARCH_CUTOFF";

    private readonly AppHost _host;

    private ShipLeanHost(AppHost host)
    {
        _host = host;
    }

    public string BaseUrl => _host.BaseUrl;

    public IServiceProvider Services => _host.Services;

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
        var allSettings = new Dictionary<string, string?> { [SearchCutoffKey] = "0.55" };
        if (settings != null)
        {
            foreach (var (key, value) in settings)
            {
                allSettings[key] = value;
            }
        }
        return new ShipLeanHost(AppHost.Start(new AppHostOptions
        {
            ConnectionString = connectionString,
            ListenOnAllInterfaces = listenOnAllInterfaces,
            RedisConnection = redisConnection,
            Settings = allSettings,
            Profile = HostProfile.Test,
            ConfigureServices = services =>
            {
                // Before the fixture's own services, so a fixture can still swap in its own resolver. No test looks a name up on the real network.
                services.Replace(ServiceDescriptor.Singleton<IHostResolver>(new NoNetworkHostResolver()));
                // Likewise the embedder: the fake is deterministic and needs no model. A fixture opts back into the real factory with EmbedderServices.Register.
                services.Replace(ServiceDescriptor.Singleton<IEmbedder>(new FakeEmbedder()));
                if (user != null)
                {
                    TestAuthentication.Add(services, user);
                }
                configureTestServices?.Invoke(services);
            },
        }));
    }

    // Whether `pnpm run build` has emitted the SPA into the WebApi's wwwroot.
    public static bool HasClientBundle() => File.Exists(Path.Combine(RepoLayout.WebApiProject, "wwwroot", "index.html"));

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
            .RegisterAsync(new OAuthClientRegistration(clientName, ["http://127.0.0.1:8123/callback"]), CancellationToken.None);
        var authorizationId = await provider.GetRequiredService<IAgentAuthorizations>()
            .CreateAsync(user.Id ?? throw new ArgumentException("An agent token needs a signed-in user.", nameof(user)), client.ClientId, scopes ?? new HashSet<string> { "shiplean:read" }, CancellationToken.None);
        return (await provider.GetRequiredService<IAgentTokenIssuer>().IssueAsync(authorizationId, resource, CancellationToken.None)).AccessToken;
    }

    // Read-only: the resource an agent token must name depends on instance_settings.base_url.
    private static async Task<string?> ReadBaseUrlAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<string>("SELECT base_url FROM instance_settings");
    }

    public HttpClient CreateClient() => _host.CreateClient();

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
