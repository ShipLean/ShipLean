namespace ShipLean.LocalHost;

public static class LocalHostCli
{
    private const string Usage =
        "Usage: dotnet run --project src/ShipLean.LocalHost -- [--port <n>] [--data fresh|keep|empty] [--database <name>] [--redis]";

    // TODO: the LocalHost runner replaces this stub.
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        await error.WriteLineAsync(Usage);
        return 2;
    }
}
