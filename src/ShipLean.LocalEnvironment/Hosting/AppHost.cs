using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Application.Identity;
using ShipLean.Application.Storage;
using ShipLean.WebApi;
using ShipLean.WebApi.Realtime;

namespace ShipLean.LocalEnvironment.Hosting;

// The real app (WebApi's Program and DI) on Kestrel, against one database. A browser can reach it, and it serves the SPA
// bundle that `pnpm run build` emitted into src/ShipLean.WebApi/wwwroot. The seams are the profile (which background
// services stay) and ConfigureServices, which runs after Program.cs has registered everything.
public sealed class AppHost : IAsyncDisposable
{
    private readonly WebApplicationFactory<ApiMarker> _factory;

    private AppHost(WebApplicationFactory<ApiMarker> factory, string baseUrl)
    {
        _factory = factory;
        BaseUrl = baseUrl;
        Stopping = factory.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
    }

    public string BaseUrl { get; }

    public IServiceProvider Services => _factory.Services;

    // The app's ApplicationStopping token.
    public CancellationToken Stopping { get; }

    public static AppHost Start(AppHostOptions options)
    {
        var factory = new WebApplicationFactory<ApiMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(RepoLayout.WebApiProject);
            // Port 0: the OS picks a free port. UseKestrel(0) alone leaves the app on the default :5000.
            builder.UseSetting(WebHostDefaults.ServerUrlsKey, Urls(options));
            // A setting, not app configuration: RealtimeServices reads it while registering, before the factory's
            // ConfigureAppConfiguration applies. Empty (in-process realtime) unless the host asks for Redis.
            builder.UseSetting(RealtimeServices.RedisConnectionKey, options.RedisConnection ?? string.Empty);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DatabaseServices.ConnectionKey] = options.ConnectionString,
                // A developer's first-admin variables must not leak into the database.
                [AdminBootstrap.EmailKey] = string.Empty,
                [AdminBootstrap.PasswordKey] = string.Empty,
                // Nor a developer's blob store: the app uses the folder unless the host passes a connection in settings.
                [BlobStoreServices.DbConnectionKey] = string.Empty,
                [BlobStoreServices.S3ConnectionKey] = string.Empty,
                // Nor a developer's webhook secrets: the endpoints stay off unless the host turns them on in settings.
                [WebhookSecrets.GithubKey] = string.Empty,
                [WebhookSecrets.GitlabKey] = string.Empty,
            }));
            if (options.Settings != null)
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(options.Settings));
            }
            builder.ConfigureTestServices(services =>
            {
                var removed = HostProfiles.RemovedHostedServices(options.Profile);
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType != null && removed.Contains(d.ImplementationType)).ToList())
                {
                    services.Remove(descriptor);
                }
            });
            if (options.ConfigureServices != null)
            {
                builder.ConfigureTestServices(options.ConfigureServices);
            }
        });
        factory.UseKestrel(0);
        factory.StartServer();
        var address = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new AppHost(factory, $"http://127.0.0.1:{new Uri(address).Port}");
    }

    public HttpClient CreateClient() => new() { BaseAddress = new Uri(BaseUrl) };

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static string Urls(AppHostOptions options)
    {
        if (options.Port > 0)
        {
            return new UriBuilder(Uri.UriSchemeHttp, "localhost", options.Port).ToString();
        }
        var address = options.ListenOnAllInterfaces ? IPAddress.Any : IPAddress.Loopback;
        return new UriBuilder(Uri.UriSchemeHttp, address.ToString(), 0).ToString();
    }
}
