using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.Asks;

// Reads a fixture from DtoFixtures/asks and checks a DTO survives the round trip through the API's JSON options. Absent and
// null differ: every field is on the wire.
internal static class AskDtoFixture
{
    public static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    public static JsonNode Read(string name)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "asks", name)))!;

    public static T Deserialize<T>(string name) => Read(name).Deserialize<T>(Options)!;

    public static bool SurvivesRoundTrip<T>(JsonNode node)
        => JsonNode.DeepEquals(node, JsonSerializer.SerializeToNode(node.Deserialize<T>(Options), Options));
}
