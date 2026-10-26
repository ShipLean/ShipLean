using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Agents;

[TestFixture]
public class ConsentDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static JsonNode Fixture() => JsonNode.Parse(File.ReadAllText(
        Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "agents", "consent.json")))!;

    private static bool RoundTrips<T>(JsonNode node) => JsonNode.DeepEquals(node, JsonSerializer.SerializeToNode(node.Deserialize<T>(Options), Options));

    [Test]
    public void ConsentDto_ShouldRoundTrip()
    {
        var fixture = Fixture();

        RoundTrips<ConsentRequestDto>(fixture["request"]!).ShouldBeTrue();
        RoundTrips<ConsentRequestDto>(fixture["unverifiedRequest"]!).ShouldBeTrue();
        RoundTrips<ConsentDecision>(fixture["decision"]!).ShouldBeTrue();
        RoundTrips<ConsentDecision>(fixture["denial"]!).ShouldBeTrue();
        RoundTrips<ConsentResult>(fixture["result"]!).ShouldBeTrue();
        RoundTrips<List<ConnectedAgentDto>>(fixture["connected"]!).ShouldBeTrue();
    }

    [Test]
    public void ConsentDecision_WhenAllowMissing_ShouldNotBind()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ConsentDecision>("""{"scopes":[]}""", Options));
    }
}
