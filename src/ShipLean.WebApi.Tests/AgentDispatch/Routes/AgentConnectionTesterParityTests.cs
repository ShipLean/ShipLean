using System.Net;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.Agents.Test;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.AgentReceiver;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Instance.AgentConnections;

namespace ShipLean.WebApi.Tests.AgentDispatch.Routes;

// The tester can't reference the dispatch code (features may not reference each other), so it carries its own copy of the client name and the
// signature. This is where the two are held together.
[TestFixture]
public class AgentConnectionTesterParityTests : ApiFixture
{
    [Test]
    public void Tester_ShouldUseTheDeliverersClientAndHeaderNames()
    {
        AgentConnectionTester.HttpClientName.ShouldBe(AgentDeliverer.HttpClientName);
        AgentConnectionTester.SignatureHeader.ShouldBe(LinearSignature.Header);
    }

    [Test]
    public async Task Ping_ShouldCarryTheSignatureTheDelivererWouldComputeForTheSameBytes()
    {
        await using var receiver = new TestAgentReceiver();
        var issued = await AgentConnectionsWire.CreateAsync(Api, $"Parity {Guid.NewGuid():N}", receiver.Url);

        using var response = await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{issued.Connection.Id}/test");

        response.IsSuccessStatusCode.ShouldBeTrue();
        var ping = receiver.Received.ShouldHaveSingleItem();
        ping.Headers[LinearSignature.Header].ShouldBe(LinearSignature.Sign(issued.SigningSecret!, ping.Body));
    }

    // Everything else about the request. The tester writes its own headers and body fields, so a drift on either side (the deliverer moves
    // the timestamp to seconds, or a relay starts to require one more header) would make the relay refuse the ping and Reached would still be
    // true. One real delivery and one ping go to the same receiver and are compared field by field.
    [Test]
    public async Task Ping_ShouldCarryTheSameHeaderNamesTimestampAndBodyFieldsAsARealDelivery()
    {
        await using var receiver = new TestAgentReceiver();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE agent_delivery SET status = 'failed', last_error = 'Set aside by a test.' WHERE status = 'pending'");
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var connectionId = arranged.ConnectionId!.Value;
        await database.ExecuteAsync("UPDATE agent_connection SET receiver_url = @url WHERE id = @connectionId", new { url = receiver.Url, connectionId });
        var item = await arranged.CreateItemAsync(Api);
        using var started = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        started.StatusCode.ShouldBe(HttpStatusCode.OK, await started.Content.ReadAsStringAsync());
        await database.ExecuteAsync(
            "UPDATE agent_delivery SET next_attempt_at = now() - interval '1 minute' WHERE session_id IN (SELECT id FROM agent_session WHERE work_item_id = @id)", new { id = item.Id });

        var deliverer = new AgentDeliverer(
            Host.Services.GetRequiredService<NpgsqlDataSource>(),
            Host.Services.GetRequiredService<IAgentConnections>(),
            Host.Services.GetRequiredService<IHttpClientFactory>(),
            Host.Services.GetRequiredService<AgentSessionNotifier>(),
            Host.Services.GetRequiredService<IAgentDeliveryAttempts>(),
            TimeProvider.System,
            NullLogger<AgentDeliverer>.Instance);
        await deliverer.RunDueAsync(CancellationToken.None);
        using var response = await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{connectionId}/test");
        response.IsSuccessStatusCode.ShouldBeTrue();

        receiver.Received.Count.ShouldBe(2);
        var (delivery, ping) = (receiver.Received[0], receiver.Received[1]);
        JsonNode.Parse(delivery.Body)!["action"]!.GetValue<string>().ShouldBe("created");
        JsonNode.Parse(ping.Body)!["type"]!.GetValue<string>().ShouldBe("ping");

        // traceparent and tracestate come from whichever activity is current when the client sends (the ping runs inside a request, the delivery does not).
        static IEnumerable<string> Names(TestAgentReceiver.ReceivedWebhook sent)
            => sent.Headers.Keys.Where(k => !k.Equals("traceparent", StringComparison.OrdinalIgnoreCase) && !k.Equals("tracestate", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase);

        Names(ping).ShouldBe(Names(delivery));
        foreach (var name in new[] { "Linear-Signature", "Linear-Timestamp", "Linear-Delivery", "User-Agent", "Content-Type" })
        {
            ping.Headers.ShouldContainKey(name);
            delivery.Headers.ShouldContainKey(name);
        }

        ping.Headers["User-Agent"].ShouldBe(delivery.Headers["User-Agent"]);
        ping.Headers["Content-Type"].ShouldBe(delivery.Headers["Content-Type"]);
        Guid.TryParse(ping.Headers["Linear-Delivery"], out _).ShouldBeTrue();
        Guid.TryParse(delivery.Headers["Linear-Delivery"], out _).ShouldBeTrue();

        // Milliseconds since the epoch as plain digits, in the header and in the body, on both sides; both were sent within seconds of each other.
        var stamps = new List<long>();
        foreach (var sent in new[] { delivery, ping })
        {
            var header = sent.Headers["Linear-Timestamp"];
            header.ShouldMatch("^[0-9]{13}$");
            var bodyStamp = JsonNode.Parse(sent.Body)!["webhookTimestamp"]!.GetValue<long>();
            bodyStamp.ToString(System.Globalization.CultureInfo.InvariantCulture).ShouldBe(header);
            JsonNode.Parse(sent.Body)!["webhookId"]!.GetValue<string>().ShouldBe(connectionId.ToString());
            stamps.Add(bodyStamp);
        }

        Math.Abs(stamps[0] - stamps[1]).ShouldBeLessThan(60_000);
    }
}
