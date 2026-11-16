using System.Net;
using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.AgentReceiver;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// An instance admin adds an agent connection whose receiver is a real loopback socket answering 502 (a relay that refuses the signed ping), opens
// Instance settings > Agents and presses Test connection. The row says what the receiver answered, 502 BadGateway, and the receiver got one signed ping.
[TestFixture]
public class AgentConnectionHealthJourneyTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> Client = new() { ["X-ShipLean-Client"] = "agent-connection-health-journey" };

    private async Task<JsonElement> ApiAsync(string method, string path, object? body = null)
    {
        var options = new APIRequestContextOptions { Method = method, Headers = Client, DataObject = body };
        var response = await Page.Context.APIRequest.FetchAsync(path, options);
        var text = await response.TextAsync();
        response.Ok.ShouldBeTrue(text);
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    [Test]
    public async Task TestConnection_WhenReceiverRefuses_ShouldShowTheStatus()
    {
        // Arrange: a receiver that answers 502, and a connection pointing at it.
        await using var receiver = new TestAgentReceiver(HttpStatusCode.BadGateway);
        var issued = await ApiAsync("POST", "/api/instance/agent-connections", new { name = "Refusing relay", receiverUrl = receiver.Url });
        var signingSecret = issued.GetProperty("signingSecret").GetString()!;

        // Act: Test connection on the row.
        await Page.GotoAsync(Links.InstanceSettings("agents"));
        var row = Page.GetByTestId(SettingsTestIds.AgentsRow);
        await Expect(row).ToHaveCountAsync(1);
        await row.GetByTestId(SettingsTestIds.AgentsConnectionTest).ClickAsync();

        // Assert: the row shows the status the receiver answered, not an all-clear, and the receiver got one signed ping.
        var result = row.GetByTestId(SettingsTestIds.AgentsConnectionTestResult);
        await Expect(result).ToHaveTextAsync("Answered 502 BadGateway");
        await Expect(row.GetByTestId(SettingsTestIds.AgentsConnectionTest)).ToBeEnabledAsync();
        receiver.Received.ShouldHaveSingleItem().IsSignedWith(signingSecret).ShouldBeTrue();
    }
}
