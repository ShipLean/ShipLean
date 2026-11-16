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

namespace ShipLean.WebApi.Tests.AgentDispatch.Troubleshooting;

// The deliverer reports every send to IAgentDeliveryAttempts (C5), with the error scrubbed once where it is built, so the attempt,
// agent_delivery.last_error and the warning log all carry the same safe text. Passes are driven by hand with a fake clock, a stub
// receiver and a recording double; sessions are opened through Start agent, like AgentDeliveryTests.
[TestFixture]
public class AgentDeliveryAttemptRecordingTests : ApiFixture
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 4, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider _clock = null!;
    private StubReceiver _receiver = null!;
    private RecordingAttempts _attempts = null!;
    private RecordedLogs _logs = null!;

    [TearDown]
    public void DisposeReceiver() => _receiver.Dispose();

    // The database is shared by the class, so rows left pending by an earlier test are set aside: a pass sends everything due.
    [SetUp]
    public async Task Arrange()
    {
        _clock = new FakeTimeProvider(Start);
        _receiver = new StubReceiver();
        _attempts = new RecordingAttempts();
        _logs = new RecordedLogs();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE agent_delivery SET status = 'failed', last_error = 'Set aside by a test.' WHERE status = 'pending'");
    }

    private AgentDeliverer Deliverer()
        => new(
            Host.Services.GetRequiredService<NpgsqlDataSource>(),
            Host.Services.GetRequiredService<IAgentConnections>(),
            new StubHttpClientFactory(_receiver.Client()),
            Host.Services.GetRequiredService<AgentSessionNotifier>(),
            _attempts,
            _clock,
            new Logger<AgentDeliverer>(_logs));

    private async Task<Guid> OpenSessionAsync(NpgsqlConnection database)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var started = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        started.StatusCode.ShouldBe(HttpStatusCode.OK, await started.Content.ReadAsStringAsync());
        var session = (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem();
        await database.ExecuteAsync("UPDATE agent_delivery SET next_attempt_at = @now WHERE session_id = @id", new { now = Start, id = session.Id });
        return session.Id;
    }

    [Test]
    public async Task RunDue_WhenCreatedIsRefusedThenAccepted_ShouldRecordBothSends()
    {
        await using var database = await OpenDatabaseAsync();
        var sessionId = await OpenSessionAsync(database);
        var deliveryId = await database.QuerySingleAsync<Guid>("SELECT id FROM agent_delivery WHERE session_id = @sessionId", new { sessionId });
        _receiver.Then(HttpStatusCode.BadGateway);
        var deliverer = Deliverer();

        await deliverer.RunDueAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await deliverer.RunDueAsync(CancellationToken.None);

        _attempts.Recorded.Count.ShouldBe(2);
        var (first, second) = (_attempts.Recorded[0], _attempts.Recorded[1]);
        (first.DeliveryId, first.Attempt, first.At, first.HttpStatus, first.NextAttemptAt).ShouldBe((deliveryId, 1, Start, 502, Start.AddMinutes(1)));
        first.Error.ShouldBe(AgentErrorText.Scrub("The receiver answered 502 BadGateway."));
        (second.DeliveryId, second.Attempt, second.At, second.HttpStatus, second.Error, second.NextAttemptAt).ShouldBe((deliveryId, 2, Start.AddMinutes(1), 200, null, null));
        first.DurationMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task RunDue_WhenTheReceiverErrorHoldsAToken_ShouldScrubItForTheAttemptTheRowAndTheLog()
    {
        const string secret = "abc7f00dsecret";
        await using var database = await OpenDatabaseAsync();
        var sessionId = await OpenSessionAsync(database);
        _receiver.Then(new HttpRequestException($"Connection refused (relay.example.com:443) token={secret}"));

        await Deliverer().RunDueAsync(CancellationToken.None);

        var recorded = _attempts.Recorded.ShouldHaveSingleItem();
        recorded.HttpStatus.ShouldBeNull();
        recorded.Error.ShouldNotBeNull();
        recorded.Error.ShouldContain("[redacted]");
        recorded.Error.ShouldNotContain(secret);
        var lastError = await database.QuerySingleAsync<string>("SELECT last_error FROM agent_delivery WHERE session_id = @sessionId", new { sessionId });
        lastError.ShouldBe(recorded.Error);
        var warnings = _logs.Events.Where(e => e.StartsWith("Warning", StringComparison.Ordinal)).ToList();
        warnings.ShouldNotBeEmpty();
        warnings.ShouldAllBe(e => !e.Contains(secret));
    }

    private sealed class RecordingAttempts : IAgentDeliveryAttempts
    {
        private readonly List<AgentDeliveryAttemptCreate> _recorded = [];

        public IReadOnlyList<AgentDeliveryAttemptCreate> Recorded
        {
            get
            {
                lock (_recorded)
                {
                    return [.. _recorded];
                }
            }
        }

        public Task RecordAsync(AgentDeliveryAttemptCreate attempt, CancellationToken cancellationToken)
        {
            lock (_recorded)
            {
                _recorded.Add(attempt);
            }

            return Task.CompletedTask;
        }

        public Task<ILookup<Guid, AgentDeliveryAttemptDto>> ForSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken)
            => Task.FromResult(Array.Empty<AgentDeliveryAttemptDto>().ToLookup(_ => Guid.Empty));
    }
}
