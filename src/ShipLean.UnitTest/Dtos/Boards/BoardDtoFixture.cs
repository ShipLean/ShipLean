using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.Boards;

// Reads a board or view DTO fixture and round-trips it through the API's own serializer settings.
internal static class BoardDtoFixture
{
    public static JsonSerializerOptions Options { get; } = ApiJson.CreateOptions();

    public static JsonNode Read(string area, string file)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", area, file)))!;

    public static JsonNode ReadPinned(string file)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", file)))!;

    public static bool SurvivesRoundTrip<T>(JsonNode document)
    {
        var value = document.Deserialize<T>(Options);
        return JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(value, Options));
    }
}
