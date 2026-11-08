using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using ShipLean.Application.Database;
using ShipLean.Contracts.Backups;
using Npgsql;

namespace ShipLean.Application.Features.Backups.Dump;

// Writes `pg_dump --format=custom` of the SHIPLEAN_DB_CONNECTION database (decision D6). The binary is
// SHIPLEAN_BACKUP_PG_DUMP, or pg_dump on PATH.
public sealed class PgDumpDatabaseDumper(IConfiguration configuration) : IDatabaseDumper
{
    public const string BinaryKey = "SHIPLEAN_BACKUP_PG_DUMP";

    public async Task DumpAsync(Stream output, CancellationToken cancellationToken)
    {
        var connectionString = configuration[DatabaseServices.ConnectionKey]
            ?? throw new DatabaseDumpException($"{DatabaseServices.ConnectionKey} is not set.");
        var binary = string.IsNullOrWhiteSpace(configuration[BinaryKey]) ? "pg_dump" : configuration[BinaryKey]!;
        var home = Directory.CreateTempSubdirectory("shiplean-pgdump-");
        try
        {
            var startInfo = PgDumpStartInfo.Create(connectionString, binary, home.FullName, new ProcessStartInfo().Environment);
            await RunAsync(startInfo, binary, new NpgsqlConnectionStringBuilder(connectionString).Password, output, cancellationToken);
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    private static async Task RunAsync(ProcessStartInfo startInfo, string binary, string? password, Stream output, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new DatabaseDumpException(
                $"pg_dump was not found at '{binary}'. The image includes it; on a source build set {BinaryKey}.", exception);
        }

        var stderr = new PgDumpStderr(password);
        var errors = ReadErrorsAsync(process, stderr);
        try
        {
            await using (cancellationToken.Register(() => Kill(process)))
            {
                await process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
                await process.WaitForExitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await errors;
        }

        if (process.ExitCode != 0)
        {
            var detail = stderr.ToString();
            throw new DatabaseDumpException(
                $"pg_dump failed with exit code {process.ExitCode}" + (detail.Length == 0 ? "." : $": {detail}"));
        }
    }

    private static async Task ReadErrorsAsync(Process process, PgDumpStderr stderr)
    {
        while (await process.StandardError.ReadLineAsync() is { } line)
        {
            stderr.Add(line);
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }
}
