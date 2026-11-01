using System.Globalization;
using Npgsql;
using ShipLean.Application.Database;

namespace ShipLean.TestSupport.Database;

// Template databases and per-test-class clones.
//
//   shiplean_schema_<hash>_<date>  db/schema + db/migrations applied
//   shiplean_seed_<hash>_<date>    the schema template plus db/seed
//   shiplean_f_<stamp>_<id>        one test class's private copy (CREATE DATABASE ... TEMPLATE ...)
//
// <hash> identifies the db/ contents and <date> is the UTC run date (see TemplateKey), so a changed script or a new
// day means a template built once. Many test classes, processes and agents share one server, so every step that
// touches a template runs under one Postgres advisory lock (a database cannot be copied while anyone is connected to
// it, and two builders must not race to build the same template).
public static class ShipLeanDatabase
{
    private const long LockKey = 0x536869704C65;
    private const string StampFormat = "yyMMddHHmm";
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(3);
    // Taken once per process, so a run that crosses midnight keeps one template key.
    public static readonly DateOnly RunDate = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    // The first build of a template, queued behind by every other class, can take far longer than the 30 s default.
    private const int CommandTimeoutSeconds = 300;
    private static int _cleaned;

    public static Task<ClonedDatabase> CloneSeedAsync() => CloneAsync(includeSeed: true);

    public static Task<ClonedDatabase> CloneEmptyAsync() => CloneAsync(includeSeed: false);

    public static string SchemaTemplateName() => "shiplean_schema_" + TemplateKey.For(DatabaseScripts.Hash(includeSeed: false), RunDate);

    public static string SeedTemplateName() => "shiplean_seed_" + TemplateKey.For(DatabaseScripts.Hash(includeSeed: true), RunDate);

    private static async Task<ClonedDatabase> CloneAsync(bool includeSeed)
    {
        DapperTypeHandlers.Register();
        var admin = await PostgresServer.AdminConnectionStringAsync();
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SELECT pg_advisory_lock(@k)", ("k", LockKey));
        try
        {
            if (Interlocked.Exchange(ref _cleaned, 1) == 0)
            {
                await CleanUpAsync(connection);
            }
            var template = await EnsureTemplateAsync(connection, admin, includeSeed);
            var name = $"shiplean_f_{Stamp()}_{Guid.NewGuid():N}"[..30];
            await ExecuteAsync(connection, $"CREATE DATABASE \"{name}\" TEMPLATE \"{template}\"");
            var cloneConnection = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = true }.ConnectionString;
            return new ClonedDatabase(name, cloneConnection, admin);
        }
        finally
        {
            await ExecuteAsync(connection, "SELECT pg_advisory_unlock(@k)", ("k", LockKey));
        }
    }

    private static async Task<string> EnsureTemplateAsync(NpgsqlConnection connection, string admin, bool includeSeed)
    {
        var schemaTemplate = SchemaTemplateName();
        if (!await ExistsAsync(connection, schemaTemplate))
        {
            await BuildAsync(connection, admin, schemaTemplate, source: null, includeSeed: false);
        }
        if (!includeSeed)
        {
            return schemaTemplate;
        }
        var seedTemplate = SeedTemplateName();
        if (!await ExistsAsync(connection, seedTemplate))
        {
            await BuildAsync(connection, admin, seedTemplate, source: schemaTemplate, includeSeed: true);
        }
        return seedTemplate;
    }

    // Built under a scratch name and renamed, so a crashed build never leaves a half-made template behind.
    private static async Task BuildAsync(NpgsqlConnection connection, string admin, string final, string? source, bool includeSeed)
    {
        var scratch = $"shiplean_b_{Stamp()}_{Guid.NewGuid():N}"[..30];
        // The schema template pins the production locale (see FreshDatabase.Locale); the clones and the seed template
        // inherit it.
        var from = source == null
            ? $" TEMPLATE template0 ENCODING 'UTF8' LOCALE_PROVIDER libc LOCALE '{FreshDatabase.Locale}'"
            : $" TEMPLATE \"{source}\"";
        await ExecuteAsync(connection, $"CREATE DATABASE \"{scratch}\"{from}");
        var scratchConnection = new NpgsqlConnectionStringBuilder(admin) { Database = scratch, Pooling = false }.ConnectionString;
        DatabaseMigrator.Apply(scratchConnection, includeSeed);
        if (includeSeed)
        {
            // A real database has autovacuum statistics; a freshly loaded one has none. Clones copy them.
            await using var scratchDb = new NpgsqlConnection(scratchConnection);
            await scratchDb.OpenAsync();
            await ExecuteAsync(scratchDb, "ANALYZE");
        }
        await ExecuteAsync(connection, $"ALTER DATABASE \"{scratch}\" RENAME TO \"{final}\"");
    }

    // Templates dated before today (several worktrees with different db/ contents share the server on the same day,
    // so a template from today is never dropped), and clones and abandoned builds that outlived their test run.
    private static async Task CleanUpAsync(NpgsqlConnection connection)
    {
        var now = TimeProvider.System.GetUtcNow();
        var names = new List<string>();
        await using (var list = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname ~ '^shiplean_(f_[0-9]{10}|b|schema|seed)_'", connection))
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
        }
        foreach (var name in names)
        {
            var isScratch = name.StartsWith("shiplean_f_", StringComparison.Ordinal) || name.StartsWith("shiplean_b_", StringComparison.Ordinal);
            var drop = isScratch
                ? DateTimeOffset.TryParseExact(name.Split('_')[2], StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when)
                  && now - when > StaleAfter
                : TemplateKey.DateOf(name) is { } date && date < RunDate;
            if (drop)
            {
                await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
            }
        }
    }

    private static string Stamp() => TimeProvider.System.GetUtcNow().ToString(StampFormat, CultureInfo.InvariantCulture);

    private static async Task<bool> ExistsAsync(NpgsqlConnection connection, string name)
    {
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @n)", connection);
        command.Parameters.AddWithValue("n", name);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = CommandTimeoutSeconds };
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync();
    }
}
