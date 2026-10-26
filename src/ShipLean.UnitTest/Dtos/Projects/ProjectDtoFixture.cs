using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.Projects;

// Reads a DTO fixture of the projects or sprints area and round-trips it through the API's own serializer settings.
internal static class ProjectDtoFixture
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static JsonNode Read(string area, string file, params string[] path)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", area, file)))!;
        return path.Aggregate(node, (current, step) => current[step]!);
    }

    public static bool SurvivesRoundTrip<T>(JsonNode document)
    {
        var value = document.Deserialize<T>(Options);
        return JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(value, Options));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
