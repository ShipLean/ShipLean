using System.Text.Json.Nodes;
using ShipLean.Application.Features.Prompts.Sections;
using ShipLean.Contracts.Prompts;
using ShipLean.UnitTest.Dtos.Projects;

namespace ShipLean.UnitTest.Prompts;

// C4: the pinned cases in prompt-sections.json are the contract the server, the resolver and the editor's TypeScript port all obey.
[TestFixture]
public class AgentSectionsTests
{
    private static readonly string[] Agents = [PromptAgents.ClaudeCode, PromptAgents.OpenAiCli, PromptAgents.Cursor, PromptAgents.Other];

    private static IEnumerable<TestCaseData> Cases() =>
        ProjectDtoFixture.Read("prompts", "prompt-sections.json", "cases").AsArray()
            .Select(node => new TestCaseData(node!).SetArgDisplayNames(node!["name"]!.GetValue<string>()));

    private static IEnumerable<TestCaseData> ErrorCases() =>
        Cases().Where(data => ((JsonNode)data.Arguments[0]!)["problems"]!.AsArray()
            .Any(problem => problem!["kind"]!.GetValue<string>() != "unknownTag"));

    [TestCaseSource(nameof(Cases))]
    public void Filter_WhenFixtureCase_ShouldMatchEveryAgent(JsonNode fixture)
    {
        var text = fixture["text"]!.GetValue<string>();

        foreach (var agent in Agents)
        {
            AgentSections.Filter(text, agent).ShouldBe(fixture["filtered"]![agent]!.GetValue<string>(), agent);
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void Check_WhenFixtureCase_ShouldMatchTheProblems(JsonNode fixture)
    {
        var actual = AgentSections.Check(fixture["text"]!.GetValue<string>())
            .Select(problem => $"{problem.Line}|{problem.Kind}|{problem.Tag}|{problem.Suggestion}");
        var expected = fixture["problems"]!.AsArray()
            .Select(problem => $"{problem!["line"]}|{problem["kind"]!.GetValue<string>()}|{problem["tag"]}|{problem["suggestion"]}"
                .Replace("|unclosed|", "|Unclosed|").Replace("|nested|", "|Nested|").Replace("|strayClose|", "|StrayClose|").Replace("|unknownTag|", "|UnknownTag|"));

        actual.ShouldBe(expected);
    }

    [TestCaseSource(nameof(ErrorCases))]
    public void Filter_WhenTagError_ShouldGiveEveryAgentTheSameUntaggedText(JsonNode fixture)
    {
        var text = fixture["text"]!.GetValue<string>();

        var outputs = Agents.Select(agent => AgentSections.Filter(text, agent)).ToList();

        outputs.Distinct().Count().ShouldBe(1);
        outputs[0].ShouldBe(fixture["filtered"]!["other"]!.GetValue<string>());
        outputs[0].Split('\n').Intersect(LinesInsideTags(text)).ShouldBeEmpty();
    }

    // An independent walk (D21): a region starts at an open tag and ends only at the close tag of that same id (or the end of the text).
    private static List<string> LinesInsideTags(string text)
    {
        var inside = new List<string>();
        string? open = null;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (open is null && trimmed.StartsWith('<') && !trimmed.StartsWith("</", StringComparison.Ordinal))
            {
                open = trimmed.Trim('<', '>');
            }
            else if (open is not null && trimmed == $"</{open}>")
            {
                open = null;
            }
            else if (open is not null)
            {
                inside.Add(line);
            }
        }

        return inside;
    }

    [Test]
    public void Filter_When64KBOfTags_ShouldFinish()
    {
        var text = string.Concat(Enumerable.Repeat("<cursor>\nkeep?\n</cursor>\n<claude-code>\nx\n", 2000));

        var filtered = AgentSections.Filter(text, PromptAgents.Cursor);
        var checkedProblems = AgentSections.Check(text);

        text.Length.ShouldBeGreaterThan(64 * 1024 - 1);
        filtered.ShouldBeEmpty();
        checkedProblems.ShouldNotBeEmpty();
    }
}
