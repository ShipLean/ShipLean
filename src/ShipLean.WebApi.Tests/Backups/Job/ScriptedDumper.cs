using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Contracts.Backups;

namespace ShipLean.WebApi.Tests.Backups.Job;

// Writes what FakeDatabaseDumper writes, then runs the test's step (a failure, a lease loss) when one is set.
internal sealed class ScriptedDumper : IDatabaseDumper
{
    private readonly FakeDatabaseDumper _inner = new();

    public Func<Stream, CancellationToken, Task>? AfterWrite { get; set; }

    public int Calls { get; private set; }

    public async Task DumpAsync(Stream output, CancellationToken cancellationToken)
    {
        Calls++;
        await _inner.DumpAsync(output, cancellationToken);
        if (AfterWrite is { } step)
        {
            await step(output, cancellationToken);
        }
    }
}
