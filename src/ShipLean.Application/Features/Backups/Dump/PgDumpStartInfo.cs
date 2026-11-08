using System.Diagnostics;
using Npgsql;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Dump;

// Builds the pg_dump process: a cleared environment, then only what the connection string says, so nothing the host
// has set (PGSERVICE, PGOPTIONS, a passfile, ~/.pgpass) reaches the dump. The password goes in the environment, never
// on the command line.
public static class PgDumpStartInfo
{
    private const string NotSupported = "is not supported for backups";

    // Client-side settings that cannot change what pg_dump connects to.
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pooling", "Minimum Pool Size", "Maximum Pool Size", "Connection Idle Lifetime", "Connection Pruning Interval",
        "Connection Lifetime", "Timeout", "Command Timeout", "Timezone", "Application Name", "No Reset On Close",
        "Include Error Detail",
    };

    private static readonly HashSet<string> Mapped = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Port", "Database", "Username", "Password", "SSL Mode", "Root Certificate", "SSL Certificate", "SSL Key",
    };

    // inheritedEnvironment is the host's environment; only PATH, LANG and TZ are taken from it. homeFolder must be an
    // empty folder the caller owns.
    public static ProcessStartInfo Create(
        string connectionString,
        string binary,
        string homeFolder,
        IReadOnlyDictionary<string, string?> inheritedEnvironment)
    {
        var builder = ParseConnectionString(connectionString);
        RefuseUnsupported(builder);
        if (builder.Host is { } host && host.Contains(',', StringComparison.Ordinal))
        {
            throw new DatabaseDumpException($"Several hosts in the connection string {NotSupported}.");
        }

        var startInfo = new ProcessStartInfo(binary)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--format=custom");
        startInfo.ArgumentList.Add("--no-owner");
        startInfo.ArgumentList.Add("--no-privileges");

        var environment = startInfo.Environment;
        environment.Clear();
        foreach (var name in new[] { "PATH", "LANG", "TZ" })
        {
            if (inheritedEnvironment.TryGetValue(name, out var value) && value is not null)
            {
                environment[name] = value;
            }
        }
        environment["HOME"] = homeFolder;
        environment["PGPASSFILE"] = "/dev/null";
        environment["PGSERVICEFILE"] = "/dev/null";
        environment["PGSYSCONFDIR"] = homeFolder;

        Set(environment, "PGHOST", builder.Host);
        environment["PGPORT"] = builder.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Set(environment, "PGDATABASE", builder.Database);
        Set(environment, "PGUSER", builder.Username);
        Set(environment, "PGPASSWORD", builder.Password);
        environment["PGSSLMODE"] = SslMode(builder.SslMode);
        Set(environment, "PGSSLROOTCERT", builder.RootCertificate);
        Set(environment, "PGSSLCERT", builder.SslCertificate);
        Set(environment, "PGSSLKEY", builder.SslKey);
        environment["PGAPPNAME"] = "shiplean-backup";
        return startInfo;
    }

    private static NpgsqlConnectionStringBuilder ParseConnectionString(string connectionString)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            // The message of a parse failure can quote the connection string, so it is not passed on.
            throw new DatabaseDumpException("The database connection string cannot be read.");
        }
    }

    private static void RefuseUnsupported(NpgsqlConnectionStringBuilder builder)
    {
        foreach (string key in builder.Keys)
        {
            if (Mapped.Contains(key) || Ignored.Contains(key))
            {
                continue;
            }
            if (!builder.TryGetValue(key, out var value) || value is null || IsDefault(key, value))
            {
                continue;
            }
            throw new DatabaseDumpException($"The connection string setting '{key}' {NotSupported}.");
        }
    }

    private static bool IsDefault(string key, object value)
    {
        var defaults = new NpgsqlConnectionStringBuilder();
        return defaults.TryGetValue(key, out var standard)
            ? Equals(standard, value)
            : string.IsNullOrEmpty(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void Set(IDictionary<string, string?> environment, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            environment[name] = value;
        }
    }

    private static string SslMode(SslMode mode) => mode switch
    {
        Npgsql.SslMode.Disable => "disable",
        Npgsql.SslMode.Allow => "allow",
        Npgsql.SslMode.Prefer => "prefer",
        Npgsql.SslMode.Require => "require",
        Npgsql.SslMode.VerifyCA => "verify-ca",
        Npgsql.SslMode.VerifyFull => "verify-full",
        _ => throw new DatabaseDumpException($"SSL mode '{mode}' {NotSupported}."),
    };
}
