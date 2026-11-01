namespace ShipLean.LocalHost;

// Port 0 is a free port; the defaults are what `dotnet run` and F5 use.
public sealed record LocalHostOptions
{
    public const int DefaultPort = 5113;

    public const string DefaultDatabase = "shiplean_local";

    public int Port { get; init; } = DefaultPort;

    public LocalHostData Data { get; init; } = LocalHostData.Fresh;

    public string Database { get; init; } = DefaultDatabase;

    public bool Redis { get; init; }
}
