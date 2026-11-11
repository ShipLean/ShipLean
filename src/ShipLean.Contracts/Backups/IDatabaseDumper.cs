namespace ShipLean.Contracts.Backups;

// Writes a dump of the ShipLean database (pg_dump custom format) to output, then returns. It never disposes output.
// A dump that fails throws DatabaseDumpException.
public interface IDatabaseDumper
{
    Task DumpAsync(Stream output, CancellationToken cancellationToken);
}
