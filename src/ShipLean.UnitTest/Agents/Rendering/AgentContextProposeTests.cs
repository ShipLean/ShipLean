using ShipLean.Application.Features.AgentDispatch.Rendering;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Agents.Rendering;

// A propose session's `created` payload (C4): the relay reads the top-level shipleanMode to run read-only. Implement payloads
// never carry it, so the older goldens stay byte-identical.
public sealed class AgentContextProposeTests
{
    private static readonly string FixtureDirectory = Path.Combine(RepoPaths.Root, "src", "ShipLean.UnitTest", "Agents", "Rendering", "Fixtures");

    [Test]
    public void PromptCreated_WhenProposeMode_ShouldMatchGolden()
    {
        var body = AgentContextRenderer.RenderCreated(Propose());

        body["shipleanMode"]!.GetValue<string>().ShouldBe("propose");
        GoldenFor("agent-created-propose", body).ShouldBe(Actual(body));
    }

    [Test]
    public void PromptCreated_WhenImplementMode_ShouldLeaveModeOut()
    {
        var body = AgentContextRenderer.RenderCreated(Propose() with { Mode = AgentSessionMode.Implement });

        body.ContainsKey("shipleanMode").ShouldBeFalse();
    }

    // A Triage item with the issue's opening text and one comment mirrored from GitHub, and the project's proposal instructions as guidance.
    private static AgentContextInput Propose() => AgentContextSamples.Minimal() with
    {
        Item = AgentContextSamples.Minimal().Item with { Description = "The login page says \"Sing in\".\n\nReported on GitHub as ShipLean/ShipLean#64." },
        Comments = [new AgentCommentInput("9c1d2e3f-4a5b-4c6d-8e7f-0a1b2c3d4e5f", new AgentUserInput("", "GitHub @reporter", ""), AgentContextSamples.Created.AddMinutes(-5), "It is on the sign-in button too.")],
        Guidance = "Write a proposal for this GitHub issue to the project's standards. Do not change any file.",
        Mode = AgentSessionMode.Propose,
    };

    private static string Actual(System.Text.Json.Nodes.JsonObject body) => AgentContextRenderer.ToJson(body) + "\n";

    // Run with UPDATE_GOLDEN=1 to rewrite the fixture.
    private static string GoldenFor(string name, System.Text.Json.Nodes.JsonObject body)
    {
        var path = Path.Combine(FixtureDirectory, name + ".json");
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(path, Actual(body));
        }
        return File.ReadAllText(path);
    }
}
