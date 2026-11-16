using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Agents.Test;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Instance.AgentConnections;

namespace ShipLean.WebApi.Tests.AgentDispatch.Routes;

// The error a failed ping hands back goes through AgentErrorText.Scrub, like the deliverer's: a connection error that echoes the receiver URL
// (some handlers do) must not carry a token to the admin's screen, the API response or the logs.
[TestFixture]
public class AgentConnectionTesterScrubTests : ApiFixture
{
    [Test]
    public async Task Test_WhenTheConnectionErrorEchoesATokenInTheReceiverUrl_ShouldRedactItInTheResult()
    {
        const string secret = "abc7f00dsecret";
        var issued = await AgentConnectionsWire.CreateAsync(Api, $"Scrub {Guid.NewGuid():N}", $"https://relay.example.com/hook?token={secret}");
        var receiver = new StubReceiver().Then(new HttpRequestException($"Connection refused (relay.example.com:443) for https://relay.example.com/hook token={secret}"));
        var tester = new AgentConnectionTester(
            Host.Services.GetRequiredService<IAgentConnections>(), new StubHttpClientFactory(receiver.Client()), TimeProvider.System);

        var result = await tester.TestAsync(issued.Connection.Id, CancellationToken.None);

        result.Reached.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("[redacted]");
        result.Error.ShouldNotContain(secret);
        receiver.Requests.ShouldHaveSingleItem().Url.Query.ShouldContain(secret, customMessage: "the planted token was really in the URL the ping went to");
    }
}
