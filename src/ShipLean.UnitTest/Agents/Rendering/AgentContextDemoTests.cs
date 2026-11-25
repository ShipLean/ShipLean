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

    // SHIP-2279: earlier runs' comments and attachments on the demo item (an old "slides only" script) must not steer a new run.
    [Test]
    public void AsDemo_WhenItemHasCommentsAndAttachments_ShouldLeaveThemOut()
    {
        var implement = AgentContextSamples.FullWithPrompt();
        implement.Comments.ShouldNotBeEmpty();
        implement.Item.Attachments.ShouldNotBeNull().ShouldNotBeEmpty();

        var demo = implement.AsDemo();
        var context = AgentContextRenderer.RenderCreated(demo)["promptContext"]!.GetValue<string>();

        demo.Mode.ShouldBe(AgentSessionMode.Demo);
        demo.Comments.ShouldBeEmpty();
        demo.TriggerCommentId.ShouldBeNull();
        context.ShouldNotContain("Please use the existing markdown helper.");
        context.ShouldNotContain("design.md");
        context.ShouldContain(implement.Item.Title);
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
