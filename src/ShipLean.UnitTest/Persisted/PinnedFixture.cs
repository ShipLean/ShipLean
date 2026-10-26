using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Persisted;

internal static class PinnedFixture
{
    public static string Directory => Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes");

    public static JsonNode Read(string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Directory, file)))!;

    public static IEnumerable<string> Files(string pattern) =>
        System.IO.Directory.GetFiles(Directory, pattern).Select(Path.GetFileName).Order().Select(f => f!);

    // Deserialises the document, writes it back and returns the result as parsed JSON, so property order and whitespace don't matter.
    public static JsonNode RoundTrip<T>(JsonNode document)
        where T : class
    {
        var value = document.Deserialize<T>(PersistedJson.Options);
        value.ShouldNotBeNull();
        return JsonSerializer.SerializeToNode(value, PersistedJson.Options)!;
    }

    public static bool SurvivesRoundTrip<T>(JsonNode document)
        where T : class
    {
        return JsonNode.DeepEquals(document, RoundTrip<T>(document));
    }
}
