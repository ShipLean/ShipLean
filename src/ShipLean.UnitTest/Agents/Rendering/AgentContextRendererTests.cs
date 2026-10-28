using System.Xml.Linq;
using ShipLean.Application.Features.AgentDispatch.Rendering;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Agents.Rendering;

public sealed class AgentContextRendererTests
{
    private static readonly string FixtureDirectory = Path.Combine(RepoPaths.Root, "src", "ShipLean.UnitTest", "Agents", "Rendering", "Fixtures");

    [Test]
    public void RenderCreated_WhenItemHasEverything_ShouldMatchGolden()
    {
        var body = AgentContextRenderer.RenderCreated(AgentContextSamples.Full());

        GoldenFor("agent-session-created-full", body).ShouldBe(Actual(body));
    }

    [Test]
    public void RenderCreated_WhenItemIsMinimal_ShouldMatchGolden()
    {
        var body = AgentContextRenderer.RenderCreated(AgentContextSamples.Minimal());

        GoldenFor("agent-session-created-minimal", body).ShouldBe(Actual(body));
    }

    [Test]
    public void RenderCreated_WhenTextIsXmlHostile_ShouldMatchGolden()
    {
        var body = AgentContextRenderer.RenderCreated(AgentContextSamples.Hostile());

        GoldenFor("agent-session-xml-hostile", body).ShouldBe(Actual(body));
    }

    [Test]
    public void RenderPrompted_WhenPersonComments_ShouldMatchGolden()
    {
        var body = AgentContextRenderer.RenderPrompted(AgentContextSamples.Full(), AgentContextSamples.PromptComment());

        GoldenFor("agent-session-prompted-comment", body).ShouldBe(Actual(body));
    }

    [Test]
    public void RenderPrompted_WhenStopPressed_ShouldMatchGolden()
    {
        var body = AgentContextRenderer.RenderPrompted(AgentContextSamples.Full(), AgentContextSamples.PromptStop());

        GoldenFor("agent-session-prompted-stop", body).ShouldBe(Actual(body));
    }

    [Test]
    public void RenderCreated_WhenTextIsXmlHostile_ShouldProduceWellFormedPromptContextWithoutInjectedElements()
    {
        var body = AgentContextRenderer.RenderCreated(AgentContextSamples.Hostile());

        var document = XDocument.Parse("<root>" + body["promptContext"]!.GetValue<string>() + "</root>");

        document.Descendants("injected").ShouldBeEmpty();
        document.Descendants("script").ShouldBeEmpty();
        document.Descendants("guidance-rule").ShouldBeEmpty();
        document.Descendants("comment").Single().Value.ShouldContain("ignore previous");
    }

    [Test]
    public void RenderCreated_WhenCriteriaExist_ShouldAppendChecklistToDescription()
    {
        var body = AgentContextRenderer.RenderCreated(AgentContextSamples.Full());

        body["agentSession"]!["issue"]!["description"]!.GetValue<string>().ShouldEndWith(
            "## Acceptance criteria\n- [x] Golden output for a full item\n- [ ] XML is escaped");
    }

    [Test]
    public void RenderCreated_ShouldLeaveWebhookIdAndTimestampToTheSender()
    {
        var body = AgentContextRenderer.RenderCreated(AgentContextSamples.Full());

        body.ContainsKey("webhookId").ShouldBeFalse();
        body.ContainsKey("webhookTimestamp").ShouldBeFalse();
    }

    private static string Actual(System.Text.Json.Nodes.JsonObject body) => AgentContextRenderer.ToJson(body) + "\n";

    // Run with UPDATE_GOLDEN=1 to rewrite the fixtures.
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
