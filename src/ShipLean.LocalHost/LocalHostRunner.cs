using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Blobs;
using ShipLean.LocalEnvironment;
using ShipLean.LocalEnvironment.Blobs;
using ShipLean.LocalEnvironment.Database;
using ShipLean.LocalEnvironment.Hosting;

namespace ShipLean.LocalHost;

public static class LocalHostRunner
{
    public const string NoBundleWarning =
        "No client bundle in src/ShipLean.WebApi/wwwroot: run `pnpm run build` in src/ShipLean.Client to build it, "
        + "or `pnpm run dev` there to serve the client on :3000.";

    public static async Task<LocalHostInstance> StartAsync(LocalHostOptions options, string clientBundlePath, TextWriter output)
    {
        var database = await ShipLeanDatabase.OpenNamedAsync(options.Database, ModeFor(options.Data));
        var redis = options.Redis ? await RedisServer.ScenarioConnectionStringAsync() : null;
        if (!File.Exists(clientBundlePath))
        {
            await output.WriteLineAsync(NoBundleWarning);
        }
        var app = AppHost.Start(new AppHostOptions
        {
            ConnectionString = database.ConnectionString,
            Port = options.Port,
            RedisConnection = redis,
            Profile = HostProfile.Local,
        });
        try
        {
            if (options.Data != LocalHostData.Empty)
            {
                await SeedAttachmentBlobs.PutAllAsync(app.Services.GetRequiredService<IBlobStore>());
            }
            await UseOwnBaseUrlAsync(app);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
        // One write, so console logs from the indexer can't land between the lines an agent waits on.
        var newLine = output.NewLine;
        await output.WriteAsync(
            $"ShipLean is running at {app.BaseUrl}{newLine}"
            + $"Sign in as {LocalSignIn.Email} / {LocalSignIn.Password}{newLine}"
            + $"SHIPLEAN_LOCAL_URL={app.BaseUrl}{newLine}"
            + $"Press Ctrl+C to stop.{newLine}");
        return new LocalHostInstance(app);
    }

    // Links the app hands out (agent prompts, upload links, open_url) name the instance's base URL. On LocalHost that must be the
    // address it listens on, not the seed's production-looking one, or an agent following a link leaves the local run.
    private static async Task UseOwnBaseUrlAsync(AppHost app)
    {
        await using var connection = await app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync(app.Stopping);
        await connection.ExecuteAsync(new CommandDefinition("UPDATE instance_settings SET base_url = @baseUrl", new { baseUrl = app.BaseUrl }, cancellationToken: app.Stopping));
    }

    private static NamedDatabaseMode ModeFor(LocalHostData data) => data switch
    {
        LocalHostData.Keep => NamedDatabaseMode.Keep,
        LocalHostData.Empty => NamedDatabaseMode.Empty,
        _ => NamedDatabaseMode.Fresh,
    };
}
