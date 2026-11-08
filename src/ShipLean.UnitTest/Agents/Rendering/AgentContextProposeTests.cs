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
        body["shipleanRepository"]!["url"]!.GetValue<string>().ShouldBe("https://github.com/ShipLean/ShipLean");
        body["promptContext"]!.GetValue<string>().ShouldNotContain("<guidance");
        body["promptContext"]!.GetValue<string>().ShouldNotContain("<repository");
        GoldenFor("agent-created-propose", body).ShouldBe(Actual(body));
    }

    [Test]
    public void PromptCreated_WhenImplementMode_ShouldLeaveModeOut()
    {
        var body = AgentContextRenderer.RenderCreated(Propose() with { Mode = AgentSessionMode.Implement });

        body.ContainsKey("shipleanMode").ShouldBeFalse();
    }

    // A propose input as AgentContextLoader.LoadProposeAsync builds it: the issue's text from github_issue, no item url, labels,
    // criteria, parent, creator name or project name; the GitHub issue's repository as the checkout; one comment mirrored from GitHub; two related items; the round's instructions as guidance.
    private static AgentContextInput Propose()
    {
        var minimal = AgentContextSamples.Minimal();
        return minimal with
        {
            Creator = minimal.Creator with { Name = "" },
            Project = minimal.Project with { Name = "" },
            Repository = new AgentRepositoryInput("https://github.com/ShipLean/ShipLean", "main"),
            Item = new AgentItemInput(minimal.Item.Id, minimal.Item.Key, "Login button says Sing in", "The login page says \"Sing in\".\n\nIt should say \"Sign in\".", "", [], []),
            Parent = null,
            Feature = null,
            Ancestors = null,
            Comments = [new AgentCommentInput("9c1d2e3f-4a5b-4c6d-8e7f-0a1b2c3d4e5f", new AgentUserInput("", "reporter", ""), AgentContextSamples.Created.AddMinutes(-5), "It is on the sign-in button too.")],
            Guidance = "Write a proposal for this GitHub issue to the project's standards. Do not change any file.",
            Mode = AgentSessionMode.Propose,
            Propose = new AgentProposeInput("ShipLean/ShipLean", 64, "reporter",
            [
                new AgentRelatedWork("SHIP-12", "story", "Done", "Fix sign-in copy", ["https://github.com/ShipLean/ShipLean/pull/40"],
                    [new AgentMergeCommit("0a1b2c3d4e5f60718293a4b5c6d7e8f901234567", AgentContextSamples.Created.AddDays(-3))]),
                new AgentRelatedWork("SHIP-30", "bug", "New", "Sign-in page typos", [], []),
            ]),
        };
    }

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
