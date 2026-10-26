using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.WorkItems;

// Reads a work item DTO fixture and round-trips it through the API's own serializer settings.
internal static class WorkItemDtoFixture
{
    public static JsonSerializerOptions Options { get; } = ApiJson.CreateOptions();

    public static JsonNode Read(string file)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "workItems", file)))!;

    public static bool SurvivesRoundTrip<T>(JsonNode document)
    {
        var value = document.Deserialize<T>(Options);
        return JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(value, Options));
    }
}
