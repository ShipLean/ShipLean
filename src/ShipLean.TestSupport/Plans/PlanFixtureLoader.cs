using System.Text.Json.Nodes;
using ShipLean.Application.Features.Mcp.Planning.Waves;

namespace ShipLean.TestSupport.Plans;

// Reads a plan fixture from DtoFixtures/planning. A missing required field throws, naming it.
public static class PlanFixtureLoader
{
    public static PlanFixture Load(string file)
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "planning", file);
        var arguments = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var feature = arguments["feature"]!.AsObject();
        var stories = arguments["stories"]!.AsArray().Select(story =>
        {
            var wave = WaveLabel.Parse(Text(story, "wave"));
            var lane = (string?)story!["lane"];
            return new PlanFixtureStory(
                Text(story, "title"), wave, lane, Text(story, "description"),
                [.. story["tasks"]!.AsArray().Select(task => new PlanFixtureTask(
                    wave, lane, Text(task, "title"), Text(task, "brief"), Text(task, "model"), (int)task!["context_k"]!))]);
        });
        return new PlanFixture(
            arguments, new PlanFixtureFeature(Text(feature, "title"), Text(feature, "goal"), Text(feature, "description")),
            Text(arguments, "shared_branch"), Text(arguments, "common_brief"), (string?)arguments["plan_markdown"], [.. stories]);
    }

    private static string Text(JsonNode? node, string name)
        => (string?)node?[name] ?? throw new InvalidOperationException($"The plan fixture has no \"{name}\".");
}
