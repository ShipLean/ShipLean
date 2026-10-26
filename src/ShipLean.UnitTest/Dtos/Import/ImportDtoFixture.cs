using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.Import;

// Reads a DTO fixture of the import area and round-trips it through the API's serializer options.
internal static class ImportDtoFixture
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    public static T Read<T>(string file)
        => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "import", file)), Options)!;

    public static bool SurvivesRoundTrip<T>(string file, params string[] path)
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "import", file)))!;
        document = path.Aggregate(document, (current, step) => current[step]!);
        var value = document.Deserialize<T>(Options);
        return JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(value, Options));
    }
}
