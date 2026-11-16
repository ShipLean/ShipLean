using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.AgentReceiver;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Instance.AgentConnections;

namespace ShipLean.WebApi.Tests.AgentDispatch.Routes;

// C8: Test connection sends a signed ping with no item data to the connection's receiver, over the real route and the app's own delivery HttpClient.
[TestFixture]
public class AgentConnectionTestApiTests : ApiFixture
{
    private TestAgentReceiver _receiver = null!;

    [SetUp]
    public void StartReceiver() => _receiver = new TestAgentReceiver();

    [TearDown]
    public async Task StopReceiver() => await _receiver.DisposeAsync();

    private async Task<AgentConnectionIssuedDto> CreateAsync(string url)
        => await AgentConnectionsWire.CreateAsync(Api, $"Relay {Guid.NewGuid():N}", url);

    private async Task<AgentConnectionTestDto> TestAsync(Guid id)
        => (await AgentConnectionsWire.ReadAsync<AgentConnectionTestDto>(await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{id}/test"))).Value;

    [Test]
    public async Task Test_WhenReceiverAnswers_ShouldSendASignedPingWithNoItemData()
    {
        var issued = await CreateAsync(_receiver.Url);

        var result = await TestAsync(issued.Connection.Id);

        result.ShouldBe(new AgentConnectionTestDto(true, 200, null, result.DurationMs));
        var ping = _receiver.Received.ShouldHaveSingleItem();
        ping.IsSignedWith(issued.SigningSecret!).ShouldBeTrue();
        ping.IsSignedWith("another-secret").ShouldBeFalse();
        var body = JsonDocument.Parse(ping.Body).RootElement;
        body.EnumerateObject().Select(p => p.Name).Order().ShouldBe(["type", "webhookId", "webhookTimestamp"]);
        body.GetProperty("type").GetString().ShouldBe("ping");
        body.GetProperty("webhookId").GetString().ShouldBe(issued.Connection.Id.ToString());
        ping.Headers["Linear-Event"].ShouldBe("ping");
    }

    [Test]
    public async Task Test_WhenReceiverAnswers502_ShouldBeReachedWithItsStatus()
    {
        _receiver.Status = HttpStatusCode.BadGateway;
        var issued = await CreateAsync(_receiver.Url);

        var result = await TestAsync(issued.Connection.Id);

        (result.Reached, result.HttpStatus, result.Error).ShouldBe((true, 502, null));
    }

    [Test]
    public async Task Test_WhenNothingListens_ShouldNotBeReachedAndSayWhy()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var closed = $"http://127.0.0.1:{((IPEndPoint)probe.LocalEndpoint).Port}/hook";
        probe.Stop();
        var issued = await CreateAsync(closed);

        var result = await TestAsync(issued.Connection.Id);

        (result.Reached, result.HttpStatus).ShouldBe((false, null));
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Test_WhenConnectionArchived_ShouldBeAValidationError()
    {
        var issued = await CreateAsync(_receiver.Url);
        using var archived = await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{issued.Connection.Id}/archive");
        archived.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{issued.Connection.Id}/test");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        _receiver.Received.ShouldBeEmpty();
    }

    [Test]
    public async Task Test_WhenConnectionUnknown_ShouldBeNotFound()
    {
        using var response = await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{Guid.NewGuid()}/test");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Test_WhenRun_ShouldNotStoreADeliveryAttempt()
    {
        var issued = await CreateAsync(_receiver.Url);
        await TestAsync(issued.Connection.Id);

        await using var database = await OpenDatabaseAsync();
        (await database.QuerySingleAsync<int>("SELECT count(*) FROM agent_delivery_attempt WHERE connection_id = @id", new { id = issued.Connection.Id })).ShouldBe(0);
    }
}
