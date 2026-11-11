using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.TestSupport.Contracts.DeviceSessions;
using ShipLean.WebApi.Tests.Agents.Stores;
using PostgresStore = ShipLean.Application.Features.DeviceSessions.DeviceSessions;

namespace ShipLean.WebApi.Tests.DeviceSessions.Store;

// Every K4 rule against Postgres, plus the checks only a database can answer: the race, and what the tables hold.
public sealed class PostgresDeviceSessionsContractTests : DeviceSessionsContract
{
    private PostgresAgentDatabase _database = null!;
    private PostgresStore _sessions = null!;
    private Guid _userId;
    private Guid _otherUserId;

    [OneTimeSetUp]
    public async Task CloneDatabase()
    {
        _database = await PostgresAgentDatabase.CreateAsync();
        _sessions = new PostgresStore(_database.DataSource);
    }

    // Fresh users per test, so one test's sessions never show in another's list.
    [SetUp]
    public async Task InsertUsers()
    {
        _userId = await _database.InsertUserAsync();
        _otherUserId = await _database.InsertUserAsync();
    }

    [OneTimeTearDown]
    public async Task DropDatabase() => await _database.DisposeAsync();

    protected override IDeviceSessions Sessions => _sessions;

    protected override Guid UserId => _userId;

    protected override Guid OtherUserId => _otherUserId;

    private async Task<NpgsqlConnection> OpenAsync() => await _database.DataSource.OpenConnectionAsync();

    [Test]
    public async Task Exchange_WhenTwoExchangesRaceOnOneCode_ShouldIssueOneSession()
    {
        var now = TimeProvider.System.GetUtcNow();
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-race", Challenge, "Race phone", now);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exchanges = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            return await Sessions.ExchangeAsync(code, Verifier, now);
        })).ToList();
        gate.SetResult();

        var results = await Task.WhenAll(exchanges);

        results.Count(r => r != null).ShouldBe(1);
        await using var connection = await OpenAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM device_session WHERE security_stamp = 'stamp-race'")).ShouldBe(1);
    }

    [Test]
    public async Task Store_ShouldHoldNoSecretOrVerifier()
    {
        var now = TimeProvider.System.GetUtcNow();
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-clear", Challenge, "Clear phone", now);
        var signIn = (await Sessions.ExchangeAsync(code, Verifier, now)).ShouldNotBeNull();

        await using var connection = await OpenAsync();
        var rows = new List<string>();
        rows.AddRange(await connection.QueryAsync<string>("SELECT to_jsonb(t)::text FROM device_session t WHERE security_stamp = 'stamp-clear'"));
        rows.AddRange(await connection.QueryAsync<string>("SELECT to_jsonb(t)::text FROM device_sign_in_code t WHERE security_stamp = 'stamp-clear'"));
        rows.Count.ShouldBe(2);
        foreach (var row in rows)
        {
            row.ShouldNotContain(signIn.Secret);
            row.ShouldNotContain(signIn.Secret[DeviceSecret.Prefix.Length..]);
            row.ShouldNotContain(Verifier);
            row.ShouldNotContain(code);
        }
        var stored = await connection.QuerySingleAsync<byte[]>("SELECT token_hash FROM device_session WHERE id = @id", new { id = signIn.Session.Id });
        stored.ShouldBe(DeviceSecret.Hash(signIn.Secret));
        var codeStored = await connection.QuerySingleAsync<byte[]>("SELECT code_hash FROM device_sign_in_code WHERE security_stamp = 'stamp-clear'");
        codeStored.ShouldBe(DeviceSecret.Hash(code));
    }

    [Test]
    public async Task Touch_WhenWithinAMinute_ShouldNotWrite()
    {
        var now = TimeProvider.System.GetUtcNow();
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-t1", Challenge, "Touch phone", now);
        var signIn = (await Sessions.ExchangeAsync(code, Verifier, now)).ShouldNotBeNull();
        await using var connection = await OpenAsync();
        var before = await connection.QuerySingleAsync<long>("SELECT xmin::text::bigint FROM device_session WHERE id = @id", new { id = signIn.Session.Id });

        await Sessions.TouchAsync(signIn.Session.Id, now + TimeSpan.FromSeconds(59));

        (await connection.QuerySingleAsync<long>("SELECT xmin::text::bigint FROM device_session WHERE id = @id", new { id = signIn.Session.Id })).ShouldBe(before);
    }

    [Test]
    public async Task Touch_WhenOverAMinute_ShouldSlideExpiryTo90Days()
    {
        var now = TimeProvider.System.GetUtcNow();
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-t2", Challenge, "Touch phone", now);
        var signIn = (await Sessions.ExchangeAsync(code, Verifier, now)).ShouldNotBeNull();
        var later = now + TimeSpan.FromMinutes(5);

        await Sessions.TouchAsync(signIn.Session.Id, later);

        var found = (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull();
        found.ExpiresAt.ShouldBe(later + TimeSpan.FromDays(90), TimeSpan.FromMilliseconds(1));
    }
}
