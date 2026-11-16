using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Logging;

namespace ShipLean.WebApi.Tests.AgentDispatch.Attempts;

// C5: the real IAgentDeliveryAttempts.RecordAsync. It keeps one row per send, ignores a repeated (delivery, attempt), and never
// throws a database error to the sender. Passes are driven by hand with a fake clock and a stub receiver, like AgentDeliveryTests.
[TestFixture]
public class AgentDeliveryAttemptsRecordTests : ApiFixture
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 4, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider _clock = null!;
    private StubReceiver _receiver = null!;
    private RecordedLogs _logs = null!;

    [TearDown]
    public void Dispose()
    {
        _receiver.Dispose();
        _logs.Dispose();
    }

    // The database is shared by the class, so rows left pending by an earlier test are set aside: a pass sends everything due.
    [SetUp]
    public async Task Arrange()
    {
        _clock = new FakeTimeProvider(Start);
        _receiver = new StubReceiver();
        _logs = new RecordedLogs();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE agent_delivery SET status = 'failed', last_error = 'Set aside by a test.' WHERE status = 'pending'");
    }

    private AgentDeliverer Deliverer(IAgentDeliveryAttempts attempts)
        => new(
            Host.Services.GetRequiredService<NpgsqlDataSource>(),
            Host.Services.GetRequiredService<IAgentConnections>(),
            new StubHttpClientFactory(_receiver.Client()),
            Host.Services.GetRequiredService<AgentSessionNotifier>(),
            attempts,
            _clock,
            new Logger<AgentDeliverer>(_logs));

    private async Task<(Guid SessionId, Guid DeliveryId, Guid ConnectionId)> OpenSessionAsync(NpgsqlConnection database)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var started = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        started.StatusCode.ShouldBe(HttpStatusCode.OK, await started.Content.ReadAsStringAsync());
        var session = (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem();
        await database.ExecuteAsync("UPDATE agent_delivery SET next_attempt_at = @now WHERE session_id = @id", new { now = Start, id = session.Id });
        var (deliveryId, connectionId) = await database.QuerySingleAsync<(Guid, Guid)>(
            "SELECT d.id, s.connection_id FROM agent_delivery d JOIN agent_session s ON s.id = d.session_id WHERE d.session_id = @id", new { id = session.Id });
        return (session.Id, deliveryId, connectionId);
    }

    private static async Task<List<(int Attempt, DateTimeOffset At, int? HttpStatus, string? Error, DateTimeOffset? NextAttemptAt)>> RowsAsync(NpgsqlConnection database, Guid deliveryId)
        => [.. await database.QueryAsync<(int, DateTimeOffset, int?, string?, DateTimeOffset?)>(
            "SELECT attempt, at, http_status, error, next_attempt_at FROM agent_delivery_attempt WHERE delivery_id = @deliveryId ORDER BY attempt", new { deliveryId })];

    [Test]
    public async Task RunDue_WhenCreatedIsRefusedThenAccepted_ShouldKeepARowForEachSend()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, deliveryId, _) = await OpenSessionAsync(database);
        _receiver.Then(HttpStatusCode.BadGateway);
        var deliverer = Deliverer(Host.Services.GetRequiredService<IAgentDeliveryAttempts>());

        await deliverer.RunDueAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await deliverer.RunDueAsync(CancellationToken.None);

        var rows = await RowsAsync(database, deliveryId);
        rows.Count.ShouldBe(2);
        rows[0].ShouldBe((1, Start, 502, "The receiver answered 502 BadGateway.", Start.AddMinutes(1)));
        rows[1].ShouldBe((2, Start.AddMinutes(1), 200, null, null));
    }

    [Test]
    public async Task RecordAsync_WhenTheSameAttemptIsRecordedAgain_ShouldInsertNothingAndNotThrow()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, deliveryId, connectionId) = await OpenSessionAsync(database);
        var attempts = new AgentDeliveryAttempts(Host.Services.GetRequiredService<NpgsqlDataSource>(), _clock, new Logger<AgentDeliveryAttempts>(_logs));
        var first = new AgentDeliveryAttemptCreate(deliveryId, connectionId, 1, Start, 502, "Bad gateway.", 40, Start.AddMinutes(1));
        var resent = first with { At = Start.AddMinutes(1), HttpStatus = 500, Error = "Resent error text.", NextAttemptAt = null };

        await attempts.RecordAsync(first, CancellationToken.None);
        await Should.NotThrowAsync(() => attempts.RecordAsync(resent, CancellationToken.None));

        (await RowsAsync(database, deliveryId)).ShouldBe([(1, Start, 502, "Bad gateway.", Start.AddMinutes(1))]);
        _logs.Events.ShouldNotContain(e => e.Contains("Could not record", StringComparison.Ordinal), "the repeat is ignored by the insert, not swallowed after it failed");
        var warning = _logs.Events.Where(e => e.StartsWith("Warning", StringComparison.Ordinal)).ShouldHaveSingleItem();
        warning.ShouldContain(deliveryId.ToString());
        warning.ShouldContain("Attempt 1 ");
        warning.ShouldNotContain("Resent error text.");
    }

    [Test]
    public async Task RunDue_WhenRecordingFails_ShouldLogItAndStillComplete()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, deliveryId, _) = await OpenSessionAsync(database);
        await using var unreachable = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=none;Password=none;Database=none;Timeout=1;Pooling=false");
        var attempts = new AgentDeliveryAttempts(unreachable, _clock, new Logger<AgentDeliveryAttempts>(_logs));

        await Should.NotThrowAsync(() => Deliverer(attempts).RunDueAsync(CancellationToken.None));

        (await database.QuerySingleAsync<string>("SELECT status FROM agent_delivery WHERE id = @deliveryId", new { deliveryId })).ShouldBe("sent");
        (await RowsAsync(database, deliveryId)).ShouldBeEmpty();
        _logs.Events.ShouldContain(e => e.StartsWith("Warning", StringComparison.Ordinal) && e.Contains("Could not record attempt 1", StringComparison.Ordinal) && e.Contains(deliveryId.ToString(), StringComparison.Ordinal));
    }
}
