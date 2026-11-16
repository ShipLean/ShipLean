using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Agents;

// The pinned shapes of the agent panel's session (with its end and delivery log), connection health and Test connection: every
// example survives a round trip through its DTO with the API's options.
[TestFixture]
public class AgentSessionTroubleshootingDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static JsonObject Fixture(string file) => JsonNode.Parse(File.ReadAllText(
        Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "agents", file)))!.AsObject();

    private static IEnumerable<TestCaseData> Examples()
    {
        foreach (var (file, type) in new[]
        {
            ("agent-session.json", typeof(AgentSessionDto)),
            ("agent-connection-health.json", typeof(AgentConnectionHealthDto)),
            ("agent-connection-test.json", typeof(AgentConnectionTestDto)),
        })
        {
            foreach (var example in Fixture(file))
            {
                yield return new TestCaseData(file, example.Key, type).SetArgDisplayNames(file, example.Key);
            }
        }
    }

    [TestCaseSource(nameof(Examples))]
    public void Example_WhenRoundTripped_ShouldMatchThePinnedFile(string file, string example, Type type)
    {
        var document = Fixture(file)[example]!;

        var dto = document.Deserialize(type, Options);

        dto.ShouldNotBeNull();
        var written = JsonSerializer.SerializeToNode(dto, type, Options);
        JsonNode.DeepEquals(document, written).ShouldBeTrue(written?.ToJsonString());
    }

    [Test]
    public void Session_WhenDeliveryFailedAdminRead_ShouldCarryTheEndAndAttemptsOldestFirst()
    {
        var dto = Fixture("agent-session.json")["deliveryFailedAdmin"].Deserialize<AgentSessionDto>(Options)!;

        dto.EndReason.ShouldBe(AgentEndReasons.DeliveryFailed);
        dto.RunLog!.SizeBytes.ShouldBe(18432);
        dto.Deliveries.Select(d => d.Attempt).ShouldBe([1, 2]);
    }
}
