using ShipLean.LocalEnvironment;

namespace ShipLean.LocalHost;

public static class LocalHostCli
{
    public const string Usage =
        "Usage: dotnet run --project src/ShipLean.LocalHost -- [--port <n>] [--data fresh|keep|empty] [--database <name>] [--redis]";

    // 0 after a clean stop, 2 for bad arguments, 1 when startup fails.
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var parsed = LocalHostOptionsParser.Parse(args);
        if (parsed.Options == null)
        {
            await error.WriteLineAsync(parsed.Error);
            await error.WriteLineAsync(Usage);
            return 2;
        }

        var interrupted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            interrupted.TrySetResult();
        }

        Console.CancelKeyPress += OnCancelKeyPress;
        try
        {
            LocalHostInstance host;
            try
            {
                host = await LocalHostRunner.StartAsync(parsed.Options, Path.Combine(RepoLayout.WebApiProject, "wwwroot", "index.html"), output);
            }
            catch (Exception exception)
            {
                await error.WriteLineAsync(exception.Message);
                return 1;
            }

            await using (host)
            {
                var appStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await using var registration = host.Stopping.Register(() => appStopped.TrySetResult());
                await Task.WhenAny(interrupted.Task, appStopped.Task);
            }
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }
}
