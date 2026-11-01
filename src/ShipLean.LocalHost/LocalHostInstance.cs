using ShipLean.LocalEnvironment.Hosting;

namespace ShipLean.LocalHost;

// A running local host. Disposing stops the app; the database and the containers stay.
public sealed class LocalHostInstance : IAsyncDisposable
{
    private readonly AppHost _app;

    internal LocalHostInstance(AppHost app) => _app = app;

    public string BaseUrl => _app.BaseUrl;

    // Cancelled when the app starts stopping, whoever asked it to (a dispose, or SIGTERM).
    public CancellationToken Stopping => _app.Stopping;

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
