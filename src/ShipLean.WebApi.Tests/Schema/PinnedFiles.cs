using System.Text.Json.Nodes;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Schema;

// Reads the conductor's pinned JSON shapes from the repository.
internal static class PinnedFiles
{
    public static JsonNode Read(string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", file)))!;
}
