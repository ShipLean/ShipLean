using System.Diagnostics;
using System.Runtime.Versioning;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Backups.Dump;
using ShipLean.Contracts.Backups;
using ShipLean.LocalEnvironment.Database;

namespace ShipLean.WebApi.Tests.Backups.Dump;

// The pg_dump database dumper (decision D6): a custom-format dump of SHIPLEAN_DB_CONNECTION, the process it starts,
// and what it refuses. The tests that reach a server clone the seeded database; the rest need only the binary.
[TestFixture]
public class PgDumpDatabaseDumperTests
{
    private const string RestoreName = "shiplean_dump_restore";
    private const string Secret = "s3cret-pw-9f2";
    private const string ConnectionOnly = "Host=db.example;Port=5433;Database=app;Username=app;Password=" + Secret;

    private readonly List<string> _folders = [];

    [TearDown]
    public void RemoveFolders()
    {
        foreach (var folder in _folders)
        {
            Directory.Delete(folder, recursive: true);
        }
        _folders.Clear();
    }

    [Test]
    public async Task Dump_WhenRestored_ShouldHaveSameWorkItemCount()
    {
        await using var source = await ShipLeanDatabase.CloneSeedAsync();
        var target = await ShipLeanDatabase.OpenNamedAsync(RestoreName, NamedDatabaseMode.Empty);
        var file = Path.Combine(NewFolder(), "backup.dump");
        try
        {
            await using (var output = File.Create(file))
            {
                await Dumper(source.ConnectionString).DumpAsync(output, CancellationToken.None);
                output.CanWrite.ShouldBeTrue();
            }

            await RestoreAsync(target.ConnectionString, file);

            var expected = await CountWorkItemsAsync(source.ConnectionString);
            expected.ShouldBeGreaterThan(0);
            (await CountWorkItemsAsync(target.ConnectionString)).ShouldBe(expected);
        }
        finally
        {
            await ShipLeanDatabase.DropNamedAsync(RestoreName);
        }
    }

    // The shared test server trusts loopback connections, so a real wrong password is never refused here. A stand-in
    // pg_dump fails the way a refused password does and echoes the password it was given, as libpq can in a conninfo
    // error: the dumper must scrub it, and keep only the pg_dump: lines.
    [Test]
    [SupportedOSPlatform("linux")]
    public async Task Dump_WhenPasswordWrong_ShouldThrowWithoutPassword()
    {
        var script = Path.Combine(NewFolder(), "refusing-pg_dump");
        await File.WriteAllTextAsync(
            script,
            "#!/bin/sh\n"
            + "echo \"detail that is not a pg_dump line: $PGPASSWORD\" >&2\n"
            + "echo \"pg_dump: error: connection to server failed: password authentication failed (password $PGPASSWORD)\" >&2\n"
            + "exit 1\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var thrown = await Should.ThrowAsync<DatabaseDumpException>(
            () => Dumper(ConnectionOnly, script).DumpAsync(new MemoryStream(), CancellationToken.None));

        thrown.Message.ShouldContain("pg_dump: error: connection to server failed: password authentication failed");
        thrown.Message.ShouldContain("exit code 1");
        thrown.Message.ShouldNotContain(Secret);
        thrown.Message.ShouldNotContain("not a pg_dump line");
    }

    // A real refused login: the test server trusts known roles, but an unknown role is still refused.
    [Test]
    public async Task Dump_WhenRoleMissing_ShouldThrowWithoutPassword()
    {
        await using var source = await ShipLeanDatabase.CloneSeedAsync();
        const string password = "distinct-secret-7c1e4d";
        var real = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        var connection = $"Host={real.Host};Port={real.Port};Database={real.Database};Username=shiplean_no_such_role_{Guid.NewGuid():N};Password={password}";

        var thrown = await Should.ThrowAsync<DatabaseDumpException>(
            () => Dumper(connection).DumpAsync(new MemoryStream(), CancellationToken.None));

        thrown.Message.ShouldContain("pg_dump:");
        thrown.Message.ShouldNotContain(password);
    }

    [Test]
    [SupportedOSPlatform("linux")]
    public async Task Dump_WhenOutputFails_ShouldStopProcess()
    {
        var script = Path.Combine(NewFolder(), "big-pg_dump");
        await File.WriteAllTextAsync(script, "#!/bin/sh\nhead -c 1048576 /dev/zero\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // AG0032 bans Task.Wait*, so the bound is a token: a hang ends as OperationCanceledException after 30 s and fails the assertion.
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await Should.ThrowAsync<IOException>(() => Dumper(ConnectionOnly, script).DumpAsync(new FailingStream(), bound.Token));
    }

    [Test]
    public async Task Dump_WhenBinaryMissing_ShouldNameSetting()
    {
        var missing = Path.Combine(NewFolder(), "no-such-pg_dump");

        var thrown = await Should.ThrowAsync<DatabaseDumpException>(
            () => Dumper(ConnectionOnly, missing).DumpAsync(new MemoryStream(), CancellationToken.None));

        thrown.Message.ShouldBe(
            $"pg_dump was not found at '{missing}'. The image includes it; on a source build set SHIPLEAN_BACKUP_PG_DUMP.");
    }

    [Test]
    [SupportedOSPlatform("linux")]
    public async Task Dump_WhenCancelled_ShouldKillProcess()
    {
        var script = Path.Combine(NewFolder(), "slow-pg_dump");
        await File.WriteAllTextAsync(script, "#!/bin/sh\nsleep 300 </dev/null >/dev/null 2>&1 &\necho \"$$ $!\"\nwait\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var output = new SignalingStream();
        using var cancel = new CancellationTokenSource();

        var dump = Dumper(ConnectionOnly, script).DumpAsync(output, cancel.Token);
        await output.Written;
        await cancel.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => dump);
        var pids = output.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList();
        pids.Count.ShouldBe(2);
        foreach (var pid in pids)
        {
            SpinWait.SpinUntil(() => IsGone(pid), TimeSpan.FromSeconds(10)).ShouldBeTrue($"process {pid} is still running");
        }
    }

    [Test]
    public void Start_WhenBuilt_ShouldKeepPasswordOutOfArguments()
    {
        var start = Create(ConnectionOnly);

        start.ArgumentList.ShouldBe(["--format=custom", "--no-owner", "--no-privileges"]);
        start.Arguments.ShouldNotContain(Secret);
        start.Environment["PGPASSWORD"].ShouldBe(Secret);
        start.Environment["PGHOST"].ShouldBe("db.example");
        start.Environment["PGPORT"].ShouldBe("5433");
        start.Environment["PGDATABASE"].ShouldBe("app");
        start.Environment["PGUSER"].ShouldBe("app");
        start.Environment["PGAPPNAME"].ShouldBe("shiplean-backup");
    }

    [Test]
    public void Start_WhenMultipleHosts_ShouldRefuse()
    {
        var thrown = Should.Throw<DatabaseDumpException>(() => Create("Host=a.example,b.example;Database=app;Username=app"));

        thrown.Message.ShouldContain("is not supported for backups");
    }

    [Test]
    public void Start_WhenKerberosServiceSet_ShouldRefuse()
    {
        var thrown = Should.Throw<DatabaseDumpException>(
            () => Create("Host=a.example;Database=app;Username=app;Password=" + Secret + ";Kerberos Service Name=other"));

        thrown.Message.ShouldContain("is not supported for backups");
        thrown.Message.ShouldNotContain(Secret);
    }

    [Test]
    public void Start_WhenParentHasPgService_ShouldNotPassIt()
    {
        var parent = new Dictionary<string, string?>
        {
            ["PATH"] = "/usr/bin",
            ["LANG"] = "C.UTF-8",
            ["TZ"] = "UTC",
            ["PGSERVICE"] = "prod",
            ["PGOPTIONS"] = "-c statement_timeout=1",
            ["PGPASSFILE"] = "/home/someone/.pgpass",
            ["PGHOST"] = "other.example",
            ["PGSSLMODE"] = "disable",
            ["AWS_SECRET_ACCESS_KEY"] = "must-not-leak",
        };

        var start = Create(ConnectionOnly, parent: parent);

        start.Environment.ContainsKey("PGSERVICE").ShouldBeFalse();
        start.Environment.ContainsKey("PGOPTIONS").ShouldBeFalse();
        start.Environment.ContainsKey("AWS_SECRET_ACCESS_KEY").ShouldBeFalse();
        start.Environment["PGPASSFILE"].ShouldBe("/dev/null");
        start.Environment["PGSERVICEFILE"].ShouldBe("/dev/null");
        start.Environment["PGHOST"].ShouldBe("db.example");
        start.Environment["PGSSLMODE"].ShouldBe("prefer");
        start.Environment["PATH"].ShouldBe("/usr/bin");
        start.Environment["LANG"].ShouldBe("C.UTF-8");
        start.Environment["TZ"].ShouldBe("UTC");
        start.Environment.Keys.Order(StringComparer.Ordinal).ShouldBe(
        [
            "HOME", "LANG", "PATH", "PGAPPNAME", "PGDATABASE", "PGGSSENCMODE", "PGHOST", "PGPASSFILE", "PGPASSWORD", "PGPORT",
            "PGSERVICEFILE", "PGSSLMODE", "PGSYSCONFDIR", "PGUSER", "TZ",
        ]);
    }

    [Test]
    public void Start_WhenSslModeVerifyFull_ShouldMapRootCert()
    {
        var start = Create(ConnectionOnly + ";SSL Mode=VerifyFull;Root Certificate=/certs/ca.pem;SSL Certificate=/certs/c.pem;SSL Key=/certs/k.pem");

        start.Environment["PGSSLMODE"].ShouldBe("verify-full");
        start.Environment["PGSSLROOTCERT"].ShouldBe("/certs/ca.pem");
        start.Environment["PGSSLCERT"].ShouldBe("/certs/c.pem");
        start.Environment["PGSSLKEY"].ShouldBe("/certs/k.pem");
    }

    [Test]
    public void Start_WhenGssEncryptionRequired_ShouldMapIt()
    {
        Create(ConnectionOnly + ";Gss Encryption Mode=Require").Environment["PGGSSENCMODE"].ShouldBe("require");
    }

    [Test]
    public void Start_WhenHomeHasPgpass_ShouldNotReadIt()
    {
        var parentHome = NewFolder();
        File.WriteAllText(Path.Combine(parentHome, ".pgpass"), $"db.example:5433:app:app:{Secret}\n");
        var parent = new Dictionary<string, string?> { ["HOME"] = parentHome, ["PATH"] = "/usr/bin" };
        var ownHome = NewFolder();

        var start = Create("Host=db.example;Database=app;Username=app", ownHome, parent);

        start.Environment["HOME"].ShouldBe(ownHome);
        start.Environment["PGSYSCONFDIR"].ShouldBe(ownHome);
        start.Environment["PGPASSFILE"].ShouldBe("/dev/null");
        start.Environment.ContainsKey("PGPASSWORD").ShouldBeFalse();
        Directory.GetFileSystemEntries(ownHome).ShouldBeEmpty();
    }

    private ProcessStartInfo Create(string connectionString, string? home = null, IReadOnlyDictionary<string, string?>? parent = null)
        => PgDumpStartInfo.Create(connectionString, "pg_dump", home ?? NewFolder(), parent ?? new Dictionary<string, string?>());

    private static PgDumpDatabaseDumper Dumper(string connectionString, string? binary = null)
    {
        var settings = new Dictionary<string, string?> { [DatabaseServices.ConnectionKey] = connectionString };
        if (binary is not null)
        {
            settings[PgDumpDatabaseDumper.BinaryKey] = binary;
        }
        return new PgDumpDatabaseDumper(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private string NewFolder()
    {
        var folder = Directory.CreateTempSubdirectory("shiplean-dumptest-").FullName;
        _folders.Add(folder);
        return folder;
    }

    private static async Task<long> CountWorkItemsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM work_item");
    }

    private static async Task RestoreAsync(string connectionString, string file)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var start = new ProcessStartInfo("pg_restore") { RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--no-owner");
        start.ArgumentList.Add("--clean");
        start.ArgumentList.Add("--if-exists");
        start.ArgumentList.Add("--exit-on-error");
        start.ArgumentList.Add("--dbname=" + builder.Database);
        start.ArgumentList.Add(file);
        start.Environment["PGHOST"] = builder.Host;
        start.Environment["PGPORT"] = builder.Port.ToString();
        start.Environment["PGUSER"] = builder.Username;
        start.Environment["PGPASSWORD"] = builder.Password;
        using var process = Process.Start(start)!;
        var errors = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        process.ExitCode.ShouldBe(0, errors);
    }

    // A killed process may stay a zombie until its parent reaps it; that counts as gone.
    private static bool IsGone(int pid)
    {
        try
        {
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            return stat[(stat.LastIndexOf(')') + 2)..].StartsWith('Z');
        }
        catch (IOException)
        {
            return true;
        }
    }
}
