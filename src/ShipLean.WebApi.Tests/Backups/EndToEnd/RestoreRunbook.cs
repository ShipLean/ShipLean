using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipLean.Application.Features.Backups.Cli;
using ShipLean.LocalEnvironment.Database;

namespace ShipLean.WebApi.Tests.Backups.EndToEnd;

// The steps of docs/backups.md that run in a shell, done from the test: decrypt-backup and restore-blobs through
// BackupCommands.RunAsync (the entry point Program.cs calls), and pg_restore as a process. The restore target is an
// empty database on the shared test server, dropped on dispose.
internal sealed class RestoreRunbook : IAsyncDisposable
{
    private readonly string _admin;

    private RestoreRunbook(string name, string admin)
    {
        Name = name;
        _admin = admin;
        ConnectionString = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = true, Timezone = "UTC" }.ConnectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    // Runbook step 4's "CREATE DATABASE": new and empty, with the locale the app runs on, and no ShipLean tables.
    public static async Task<RestoreRunbook> CreateEmptyDatabaseAsync()
    {
        var admin = await PostgresServer.AdminConnectionStringAsync();
        var name = $"shiplean_restore_{Guid.NewGuid():N}"[..40];
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand(
            $"CREATE DATABASE \"{name}\" TEMPLATE template0 ENCODING 'UTF8' LOCALE_PROVIDER libc LOCALE '{FreshDatabase.Locale}'", connection);
        await create.ExecuteNonQueryAsync();
        return new RestoreRunbook(name, admin);
    }

    // The result of a verb: its exit code and what it printed.
    public sealed record VerbResult(int ExitCode, string Output, string Error);

    // Step 3: decrypt-backup, with the passphrase as one line of standard input.
    public static Task<VerbResult> DecryptAsync(string input, string output, string passphrase)
        => RunVerbAsync(["decrypt-backup", "--in", input, "--out", output], passphrase, new Dictionary<string, string?>());

    // Step 5 (folder store): restore-blobs into the folder SHIPLEAN_BLOB_ROOT names.
    public static Task<VerbResult> RestoreBlobsAsync(string folder, string blobRoot, string passphrase)
        => RunVerbAsync(["restore-blobs", "--in", folder], passphrase, new Dictionary<string, string?> { ["SHIPLEAN_BLOB_ROOT"] = blobRoot });

    // Step 4: pg_restore --no-owner into the empty database. The password goes in PGPASSWORD, as the dumper does.
    public async Task<(int ExitCode, string Error)> PgRestoreAsync(string dumpFile)
    {
        var admin = new NpgsqlConnectionStringBuilder(_admin);
        var startInfo = new ProcessStartInfo("pg_restore") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        startInfo.ArgumentList.Add("--no-owner");
        startInfo.ArgumentList.Add($"--host={admin.Host}");
        startInfo.ArgumentList.Add($"--port={admin.Port}");
        startInfo.ArgumentList.Add($"--username={admin.Username}");
        startInfo.ArgumentList.Add($"--dbname={Name}");
        startInfo.ArgumentList.Add(dumpFile);
        startInfo.Environment["PGPASSWORD"] = admin.Password;
        using var process = Process.Start(startInfo)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        await output;
        return (process.ExitCode, await error);
    }

    public async ValueTask DisposeAsync()
    {
        await using (var own = new NpgsqlConnection(ConnectionString))
        {
            NpgsqlConnection.ClearPool(own);
        }
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task<VerbResult> RunVerbAsync(string[] args, string passphrase, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await BackupCommands.RunAsync(args, new StringReader(passphrase + "\n"), output, error, configuration);
        return new VerbResult(exit, output.ToString(), error.ToString());
    }
}
