using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Instance.AgentConnections;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// The delivery service's passes, driven by hand with a fake clock and a stub receiver. Sessions are opened through Start agent
// (synchronous), so each test owns a project, an item, a session and its queued `created` row.
[TestFixture]
public class AgentDeliveryTests : ApiFixture
{
    private static readonly DateTimeOffset Start = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider _clock = null!;
    private StubReceiver _receiver = null!;

    [TearDown]
    public void DisposeReceiver() => _receiver.Dispose();

    // The database is shared by the class, so rows left pending by an earlier test are set aside: a pass sends everything due.
    [SetUp]
    public async Task Arrange()
    {
        _clock = new FakeTimeProvider(Start);
        _receiver = new StubReceiver();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE agent_delivery SET status = 'failed', last_error = 'Set aside by a test.' WHERE status = 'pending'");
        await database.ExecuteAsync("UPDATE agent_session SET stop_requested_at = NULL WHERE stop_requested_at IS NOT NULL");
    }

    private AgentDeliverer Deliverer(StubReceiver? receiver = null, IHttpClientFactory? factory = null)
        => new(
            Host.Services.GetRequiredService<NpgsqlDataSource>(),
            Host.Services.GetRequiredService<IAgentConnections>(),
            factory ?? new StubHttpClientFactory((receiver ?? _receiver).Client()),
            new AgentSessionNotifier(NullLogger<AgentSessionNotifier>.Instance),
            _clock,
            NullLogger<AgentDeliverer>.Instance);

    private async Task<(AgentDispatchArrange Arranged, Guid ItemId, Guid SessionId)> OpenSessionAsync(NpgsqlConnection database)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var started = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        started.StatusCode.ShouldBe(HttpStatusCode.OK, await started.Content.ReadAsStringAsync());
        var session = (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem();
        await database.ExecuteAsync("UPDATE agent_delivery SET next_attempt_at = @now WHERE session_id = @id", new { now = Start, id = session.Id });
        return (arranged, item.Id, session.Id);
    }

    private static async Task QueuePromptedAsync(NpgsqlConnection database, Guid sessionId, string text)
    {
        var body = new JsonObject
        {
            ["action"] = "prompted",
            ["type"] = "AgentSessionEvent",
            ["agentActivity"] = new JsonObject { ["content"] = new JsonObject { ["type"] = "prompt", ["body"] = text } },
        };
        await database.ExecuteAsync(
            """
            INSERT INTO agent_delivery (id, session_id, seq, action, body, next_attempt_at, status, created_at)
            SELECT @id, @sessionId, coalesce(max(seq), 0) + 1, 'prompted', @body::jsonb, @now, 'pending', @now FROM agent_delivery WHERE session_id = @sessionId
            """,
            new { id = Guid.NewGuid(), sessionId, body = body.ToJsonString(), now = Start });
    }

    private static async Task<List<Row>> RowsAsync(NpgsqlConnection database, Guid sessionId)
        => [.. await database.QueryAsync<Row>(
            "SELECT id, seq, action, status, attempts, next_attempt_at AS nextAttemptAt, last_error AS lastError FROM agent_delivery WHERE session_id = @sessionId ORDER BY seq",
            new { sessionId })];

    private static Task<string> StateAsync(NpgsqlConnection database, Guid sessionId)
        => database.QuerySingleAsync<string>("SELECT state FROM agent_session WHERE id = @sessionId", new { sessionId });

    [Test]
    public async Task RunDue_WhenCreatedIsDue_ShouldSendLinearsHeadersAndASignatureThatVerifies()
    {
        await using var database = await OpenDatabaseAsync();
        var (arranged, _, sessionId) = await OpenSessionAsync(database);

        await Deliverer().RunDueAsync(CancellationToken.None);

        var sent = _receiver.Requests.ShouldHaveSingleItem();
        var row = (await RowsAsync(database, sessionId)).ShouldHaveSingleItem();
        (row.Status, row.Attempts).ShouldBe(("sent", 1));
        sent.Url.ToString().ShouldBe("https://relay.example.com/hook");
        sent.Headers["Linear-Delivery"].ShouldBe(row.Id.ToString());
        sent.Headers["Linear-Event"].ShouldBe("AgentSessionEvent");
        sent.Headers["User-Agent"].ShouldBe("Linear-Webhook");
        sent.Headers["Content-Type"].ShouldBe("application/json; charset=utf-8");
        var secret = (await AgentConnectionsWire.GetAsync(Host, arranged.ConnectionId!.Value))!.SigningSecret;
        sent.Headers["Linear-Signature"].ShouldBe(Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), sent.Body)));
        var body = JsonNode.Parse(sent.Body)!;
        body["webhookTimestamp"]!.GetValue<long>().ShouldBe(Start.ToUnixTimeMilliseconds());
        sent.Headers["Linear-Timestamp"].ShouldBe(Start.ToUnixTimeMilliseconds().ToString());
        body["webhookId"]!.GetValue<string>().ShouldBe(arranged.ConnectionId.ToString());
        body["action"]!.GetValue<string>().ShouldBe("created");
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.Accepted)]
    [TestCase(HttpStatusCode.NoContent)]
    public async Task RunDue_WhenTheReceiverAnswersAnythingButOk_ShouldCountItAsFailed(HttpStatusCode status)
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, sessionId) = await OpenSessionAsync(database);
        _receiver.Then(status);

        await Deliverer().RunDueAsync(CancellationToken.None);

        var row = (await RowsAsync(database, sessionId)).ShouldHaveSingleItem();
        (row.Status, row.Attempts, row.NextAttemptAt).ShouldBe(("pending", 1, Start.AddMinutes(1)));
        row.LastError.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task RunDue_WhenCreatedFailsTwice_ShouldFailTheSessionWithoutWaitingForLongerRetries()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, sessionId) = await OpenSessionAsync(database);
        _receiver.Then(HttpStatusCode.BadGateway, new TaskCanceledException("timeout"));
        var deliverer = Deliverer();

        await deliverer.RunDueAsync(CancellationToken.None);
        await deliverer.RunDueAsync(CancellationToken.None);
        _receiver.Requests.Count.ShouldBe(1, "the retry is not due yet");
        _clock.Advance(TimeSpan.FromMinutes(1));
        await deliverer.RunDueAsync(CancellationToken.None);

        _receiver.Requests.Count.ShouldBe(2);
        (await RowsAsync(database, sessionId)).ShouldHaveSingleItem().Status.ShouldBe("failed");
        (await StateAsync(database, sessionId)).ShouldBe("failed");
        (await database.ExecuteScalarAsync<DateTimeOffset?>("SELECT ended_at FROM agent_session WHERE id = @sessionId", new { sessionId })).ShouldNotBeNull();
    }

    [Test]
    public async Task RunDue_WhenPromptedKeepFailing_ShouldRetryAfterOneMinuteOneHourAndSixHoursThenFailTheSession()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, sessionId) = await OpenSessionAsync(database);
        await Deliverer().RunDueAsync(CancellationToken.None);
        await QueuePromptedAsync(database, sessionId, "Please also handle empty input");
        _receiver.Then(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError);
        var deliverer = Deliverer();

        foreach (var wait in new[] { TimeSpan.Zero, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), TimeSpan.FromHours(6) })
        {
            _clock.Advance(wait);
            var before = _receiver.Requests.Count;
            await deliverer.RunDueAsync(CancellationToken.None);
            _receiver.Requests.Count.ShouldBe(before + 1);
            await deliverer.RunDueAsync(CancellationToken.None);
            _receiver.Requests.Count.ShouldBe(before + 1, "not due again until the next wait has passed");
        }

        var rows = await RowsAsync(database, sessionId);
        (rows[1].Status, rows[1].Attempts).ShouldBe(("failed", 4));
        (await StateAsync(database, sessionId)).ShouldBe("failed");
    }

    [Test]
    public async Task RunDue_WhenCreatedIsInRetry_ShouldHoldTheLaterPromptedBack()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, sessionId) = await OpenSessionAsync(database);
        await QueuePromptedAsync(database, sessionId, "Follow-up");
        _receiver.Then(HttpStatusCode.InternalServerError);
        var deliverer = Deliverer();

        await deliverer.RunDueAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(30));
        await deliverer.RunDueAsync(CancellationToken.None);

        JsonNode.Parse(_receiver.Requests.ShouldHaveSingleItem().Body)!["action"]!.GetValue<string>().ShouldBe("created");
        _clock.Advance(TimeSpan.FromSeconds(30));
        await deliverer.RunDueAsync(CancellationToken.None);
        _receiver.Requests.Select(r => JsonNode.Parse(r.Body)!["action"]!.GetValue<string>()).ShouldBe(["created", "created", "prompted"]);
    }

    [Test]
    public async Task RunDue_WhenANewInstanceStartsWithPendingRows_ShouldSendThem()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, sessionId) = await OpenSessionAsync(database);
        await QueuePromptedAsync(database, sessionId, "Queued while the old process was down");

        await Deliverer().RunDueAsync(CancellationToken.None);

        _receiver.Requests.Count.ShouldBe(2);
        (await RowsAsync(database, sessionId)).Select(r => r.Status).ShouldBe(["sent", "sent"]);
    }

    [Test]
    public async Task RunDue_WhenTwoInstancesRunTogether_ShouldSendEachRowOnce()
    {
        await using var database = await OpenDatabaseAsync();
        var sessions = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var (_, _, sessionId) = await OpenSessionAsync(database);
            await QueuePromptedAsync(database, sessionId, $"Follow-up {i}");
            sessions.Add(sessionId);
        }

        await Task.WhenAll(Deliverer().RunDueAsync(CancellationToken.None), Deliverer().RunDueAsync(CancellationToken.None));

        var ids = _receiver.Requests.Select(r => r.Headers["Linear-Delivery"]).ToList();
        ids.Count.ShouldBe(8);
        ids.Distinct().Count().ShouldBe(8);
        foreach (var sessionId in sessions)
        {
            (await RowsAsync(database, sessionId)).Select(r => r.Status).ShouldBe(["sent", "sent"]);
        }
    }

    [Test]
    public async Task RunDue_WhenARowIsClaimedByAnotherSender_ShouldWaitForItsLease()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, sessionId) = await OpenSessionAsync(database);
        await database.ExecuteAsync("UPDATE agent_delivery SET claimed_until = @until WHERE session_id = @sessionId", new { sessionId, until = Start.AddSeconds(20) });
        var deliverer = Deliverer();

        await deliverer.RunDueAsync(CancellationToken.None);
        _receiver.Requests.ShouldBeEmpty();
        _clock.Advance(TimeSpan.FromSeconds(21));
        await deliverer.RunDueAsync(CancellationToken.None);

        _receiver.Requests.ShouldHaveSingleItem();
    }

    [Test]
    public async Task RunDue_WhenPromptedIsPendingForAnEndedSession_ShouldDropItWithoutSending()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, sessionId) = await OpenSessionAsync(database);
        await Deliverer().RunDueAsync(CancellationToken.None);
        await QueuePromptedAsync(database, sessionId, "Too late");
        await database.ExecuteAsync("UPDATE agent_session SET state = 'completed', ended_at = @Start WHERE id = @sessionId", new { sessionId, Start });

        await Deliverer().RunDueAsync(CancellationToken.None);

        _receiver.Requests.Count.ShouldBe(1);
        (await RowsAsync(database, sessionId))[1].Status.ShouldBe("failed");
    }

    [Test]
    public async Task RunDue_WhenTheReceiverRedirects_ShouldNotFollowAndCountItAsFailed()
    {
        await using var database = await OpenDatabaseAsync();
        var (arranged, _, sessionId) = await OpenSessionAsync(database);
        await using var target = new LocalReceiver(HttpStatusCode.OK);
        await using var redirector = new LocalReceiver(HttpStatusCode.Redirect, target.Url);
        await database.ExecuteAsync("UPDATE agent_connection SET receiver_url = @url WHERE id = @id", new { url = redirector.Url, id = arranged.ConnectionId });

        await Deliverer(factory: Host.Services.GetRequiredService<IHttpClientFactory>()).RunDueAsync(CancellationToken.None);

        redirector.Hits.ShouldBe(1);
        target.Hits.ShouldBe(0);
        (await RowsAsync(database, sessionId)).ShouldHaveSingleItem().Status.ShouldBe("pending");
    }

    [Test]
    public async Task RunDue_WhenTheRealClientSends_ShouldReachTheReceiverWithTheSameHeaders()
    {
        await using var database = await OpenDatabaseAsync();
        var (arranged, _, sessionId) = await OpenSessionAsync(database);
        await using var receiver = new LocalReceiver(HttpStatusCode.OK);
        await database.ExecuteAsync("UPDATE agent_connection SET receiver_url = @url WHERE id = @id", new { url = receiver.Url, id = arranged.ConnectionId });

        await Deliverer(factory: Host.Services.GetRequiredService<IHttpClientFactory>()).RunDueAsync(CancellationToken.None);

        receiver.Hits.ShouldBe(1);
        receiver.LastHeaders["Linear-Event"].ShouldBe("AgentSessionEvent");
        receiver.LastHeaders["User-Agent"].ShouldBe("Linear-Webhook");
        (await RowsAsync(database, sessionId)).ShouldHaveSingleItem().Status.ShouldBe("sent");
    }

    [Test]
    public async Task CancelUnansweredStops_WhenTenMinutesPassed_ShouldCancelTheSessionOnceAndDropItsPendingRows()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, sessionId) = await OpenSessionAsync(database);
        await database.ExecuteAsync("UPDATE agent_session SET stop_requested_at = @Start WHERE id = @sessionId", new { sessionId, Start });
        await QueuePromptedAsync(database, sessionId, "stop");
        var deliverer = Deliverer();

        _clock.Advance(TimeSpan.FromMinutes(9));
        (await deliverer.CancelUnansweredStopsAsync(CancellationToken.None)).ShouldBe(0);
        (await StateAsync(database, sessionId)).ShouldBe("submitted");
        _clock.Advance(TimeSpan.FromMinutes(1));
        (await deliverer.CancelUnansweredStopsAsync(CancellationToken.None)).ShouldBe(1);
        (await deliverer.CancelUnansweredStopsAsync(CancellationToken.None)).ShouldBe(0);

        (await StateAsync(database, sessionId)).ShouldBe("canceled");
        (await database.ExecuteScalarAsync<DateTimeOffset?>("SELECT ended_at FROM agent_session WHERE id = @sessionId", new { sessionId })).ShouldBe(Start.AddMinutes(10));
        (await RowsAsync(database, sessionId)).Select(r => r.Status).ShouldBe(["failed", "failed"]);
    }

    [Test]
    public async Task CancelUnansweredStops_WhenNoStopWasRequested_ShouldLeaveTheSessionAlone()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, sessionId) = await OpenSessionAsync(database);

        _clock.Advance(TimeSpan.FromHours(2));
        await Deliverer().CancelUnansweredStopsAsync(CancellationToken.None);

        (await StateAsync(database, sessionId)).ShouldBe("submitted");
    }

    private sealed record Row(Guid Id, int Seq, string Action, string Status, int Attempts, DateTimeOffset NextAttemptAt, string? LastError);
}
