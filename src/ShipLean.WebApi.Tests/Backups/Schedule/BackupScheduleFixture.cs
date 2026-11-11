using Dapper;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Backups;
using ShipLean.LocalEnvironment.Database;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// A database per test (empty unless Seeded) (the lease is one lock per database), a fake clock at Start, a saved hourly config, a
// probe job, and ScheduleInstances over them. Everything an instance started is stopped at tear-down.
public abstract class BackupScheduleFixture
{
    protected const string HourlyCron = "0 * * * *";

    protected static readonly DateTimeOffset Start = new(2026, 10, 24, 14, 0, 0, TimeSpan.Zero);

    private readonly List<ScheduleInstance> _instances = [];
    private ClonedDatabase _database = null!;

    // True clones the seed (its users can start a manual run) instead of the empty schema.
    protected virtual bool Seeded => false;

    protected NpgsqlDataSource DataSource { get; private set; } = null!;

    protected FakeTimeProvider Time { get; private set; } = null!;

    protected SettableConfigReader Configs { get; private set; } = null!;

    protected JobProbe Probe { get; private set; } = null!;

    [SetUp]
    public async Task CloneDatabase()
    {
        DapperTypeHandlers.Register();
        _database = Seeded ? await ShipLeanDatabase.CloneSeedAsync() : await ShipLeanDatabase.CloneEmptyAsync();
        DataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        Time = new FakeTimeProvider(Start);
        Configs = new SettableConfigReader(Config());
        Probe = new JobProbe();
    }

    [TearDown]
    public async Task DropDatabase()
    {
        Probe.Gate?.TrySetResult();
        foreach (var instance in _instances)
        {
            await instance.DisposeAsync();
        }
        _instances.Clear();
        await DataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected static BackupConfig Config(
        bool enabled = true,
        string cron = HourlyCron,
        string bucket = "shiplean-backups",
        string passphrase = "correct horse battery staple",
        int generation = 1,
        DateTimeOffset? updatedAt = null)
    {
        var destination = new BackupDestinationConfig("s3", new S3DestinationConfig("us-east-1", null, bucket, "shiplean/", false, null, null));
        return new BackupConfig(enabled, destination, BackupDestinationId.Of(destination), cron, 14, false, false, passphrase, generation, updatedAt ?? Start);
    }

    protected ScheduleInstance NewInstance()
    {
        var instance = new ScheduleInstance(DataSource, _database.ConnectionString, new WatchedClock(Time), Configs, Probe);
        _instances.Add(instance);
        return instance;
    }

    // Stops an instance before the test ends, as a restart would.
    protected async Task StopAsync(ScheduleInstance instance)
    {
        _instances.Remove(instance);
        await instance.DisposeAsync();
    }

    // Every backup_run row, oldest first, Skipped rows included.
    protected async Task<IReadOnlyList<RunRow>> RowsAsync()
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        var rows = await connection.QueryAsync<RunRow>(
            "SELECT trigger AS Trigger, state AS State, scheduled_for AS ScheduledFor, started_at AS StartedAt, error AS Error, " +
            "passphrase_generation AS PassphraseGeneration, destination_id AS DestinationId FROM backup_run ORDER BY started_at, scheduled_for");
        return rows.ToList();
    }

    protected sealed record RunRow(string Trigger, string State, DateTimeOffset? ScheduledFor, DateTimeOffset StartedAt, string? Error, int PassphraseGeneration, string? DestinationId);
}
