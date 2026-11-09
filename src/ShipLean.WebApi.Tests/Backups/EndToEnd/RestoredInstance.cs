using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Backups.EndToEnd;

// A second app on what the runbook restored: the restored database and the restored blob folder. Disposing stops it and
// drops the database.
public sealed class RestoredInstance(RestoreRunbook database, string blobRoot, ShipLeanHost host) : IAsyncDisposable
{
    public RestoreRunbook Database => database;

    public string BlobRoot => blobRoot;

    public ApiClient Api { get; } = new(host.CreateClient());

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync();
        await database.DisposeAsync();
    }
}
