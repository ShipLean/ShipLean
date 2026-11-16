using System.Text.Json;
using ShipLean.Contracts.Inspection;

namespace ShipLean.TestSupport.Inspection;

// The fixture pairs under src/ShipLean.TestSupport/Inspection/Fixtures: <name>.otlp.json, <name>.expected.json and, for the
// synthetic ones, <name>.assembled.json. Synthetic lists the top folder only; Recorded lists Recorded/ (real agent runs).
public static class InspectionFixtures
{
    public static string Folder => Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "Inspection", "Fixtures");

    public static IReadOnlyList<InspectionFixture> Synthetic => Load(Folder);

    public static IReadOnlyList<InspectionFixture> Recorded => Load(Path.Combine(Folder, "Recorded"));

    public static InspectionFixture Get(string name) => Synthetic.Concat(Recorded).Single(f => f.Name == name);

    private static List<InspectionFixture> Load(string folder)
        => !Directory.Exists(folder)
            ? []
            : [.. Directory.EnumerateFiles(folder, "*.expected.json").Order(StringComparer.Ordinal).Select(Read)];

    private static InspectionFixture Read(string expectedPath)
    {
        var stem = expectedPath[..^".expected.json".Length];
        using var document = JsonDocument.Parse(File.ReadAllText(expectedPath));
        var keys = document.RootElement.GetProperty("compareKeys").EnumerateArray().Select(k => k.GetString()!).ToList();
        var run = document.RootElement.GetProperty("run").Deserialize<RawRun>(RawRunJson.Options)!;
        return new InspectionFixture(Path.GetFileName(stem), stem + ".otlp.json", stem + ".assembled.json", keys, run);
    }
}
