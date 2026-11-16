using System.Text.Json.Nodes;
using ShipLean.Application.Features.AgentDispatch.Rendering;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Agents.Rendering;

// C1: a sprint-demo session's `created` payload is the implement one plus the top-level shipleanMode "demo", which the relay reads to run
// the demo tool profile. It has no shipleanRepository (the relay resolves the repository as for implement), and implement payloads stay
// byte-identical to their goldens.
public sealed class AgentContextDemoTests
{
    private static readonly string FixtureDirectory = Path.Combine(RepoPaths.Root, "src", "ShipLean.UnitTest", "Agents", "Rendering", "Fixtures");

    [Test]
    public void PromptCreated_WhenDemoMode_ShouldCarryShipleanModeDemo()
    {
        var implement = AgentContextSamples.FullWithPrompt();

        var body = AgentContextRenderer.RenderCreated(implement with { Mode = AgentSessionMode.Demo });

        body["shipleanMode"]!.GetValue<string>().ShouldBe("demo");
        body.ContainsKey("shipleanRepository").ShouldBeFalse();
        body["promptContext"]!.GetValue<string>().ShouldBe(AgentContextRenderer.RenderCreated(implement)["promptContext"]!.GetValue<string>());
        Without(body, "shipleanMode").ShouldBe(AgentContextRenderer.ToJson(AgentContextRenderer.RenderCreated(implement)));
    }

    [Test]
    public void PromptCreated_WhenImplement_ShouldHaveNoShipleanMode()
    {
        var body = AgentContextRenderer.RenderCreated(AgentContextSamples.Full());

        body.ContainsKey("shipleanMode").ShouldBeFalse();
        (AgentContextRenderer.ToJson(body) + "\n").ShouldBe(File.ReadAllText(Path.Combine(FixtureDirectory, "agent-session-created-full.json")));
    }

    private static string Without(JsonObject body, string field)
    {
        var copy = body.DeepClone().AsObject();
        copy.Remove(field);
        return AgentContextRenderer.ToJson(copy);
    }
}
