using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.Agents.Test;
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
}
