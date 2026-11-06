using System.Text;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Fakes.Backups.Engine;

// Stands in for pg_dump: writes PGDMP-FAKE and leaves output open.
public sealed class FakeDatabaseDumper : IDatabaseDumper
{
    public const string Content = "PGDMP-FAKE";

    public Task DumpAsync(Stream output, CancellationToken cancellationToken)
        => output.WriteAsync(Encoding.ASCII.GetBytes(Content), cancellationToken).AsTask();
}
