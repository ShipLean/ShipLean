using ShipLean.WebApi.Infrastructure.PlanningGuide;
using System.Text.RegularExpressions;
using ShipLean.Application.Features.Demos.Prompt;
using ShipLean.Application.Features.IssueIntake.Settings;
using ShipLean.Contracts.Prompts;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.UnitTest.Prompts;

[TestFixture]
public class BuiltInPromptsTests
{
    private readonly BuiltInPrompts _prompts = new();

    private static IEnumerable<TestCaseData> GuideParts()
    {
        yield return new TestCaseData(PromptKey.Work, PlanGuidePart.Work);
        yield return new TestCaseData(PromptKey.Guide, PlanGuidePart.Guide);
        yield return new TestCaseData(PromptKey.PlanReviewer, PlanGuidePart.PlanReviewer);
        yield return new TestCaseData(PromptKey.Lead, PlanGuidePart.Lead);
        yield return new TestCaseData(PromptKey.Brief, PlanGuidePart.Brief);
        yield return new TestCaseData(PromptKey.Report, PlanGuidePart.Report);
        yield return new TestCaseData(PromptKey.CodeReviewer, PlanGuidePart.CodeReviewer);
        yield return new TestCaseData(PromptKey.Fix, PlanGuidePart.Fix);
        yield return new TestCaseData(PromptKey.Retro, PlanGuidePart.Retro);
    }

    [Test]
    public void All_ShouldListElevenPromptsInStageOrder()
    {
        _prompts.All.Select(facts => facts.Key).ShouldBe(Enum.GetValues<PromptKey>());
        _prompts.All.Select(facts => facts.Stage).ShouldBe(_prompts.All.Select(facts => facts.Stage).Order());
        _prompts.Stages.Select(stage => stage.Id).ShouldBe(Enum.GetValues<PromptStage>());
        _prompts.BuiltInOnly.Count.ShouldBe(2);
    }

    [TestCaseSource(nameof(GuideParts))]
    public void Text_WhenGuidePart_ShouldEqualPlanGuideRead(PromptKey key, PlanGuidePart part)
    {
        _prompts.Text(key).ShouldBe(PlanGuide.Read(part));
    }

    [Test]
    public void Text_WhenSprintDemo_ShouldEqualTheEmbeddedTemplate()
    {
        using var stream = typeof(SprintDemoPrompt).Assembly.GetManifestResourceStream("sprint-demo/sprint-demo-prompt.md")!;
        using var reader = new StreamReader(stream);

        _prompts.Text(PromptKey.SprintDemo).ShouldBe(reader.ReadToEnd());
    }

    [Test]
    public void Text_WhenIssueProposal_ShouldEqualTheServedDefault()
    {
        _prompts.Text(PromptKey.IssueProposal).ShouldBe(IssueIntakeSettingsService.DefaultInstructions);
    }

    [Test]
    public void Placeholders_WhenBrief_ShouldMatchPlanPlaceholders()
    {
        _prompts.Facts(PromptKey.Brief).Placeholders.ShouldBe(PlanPlaceholders.Brief);
        _prompts.Facts(PromptKey.Lead).Placeholders.ShouldBe(PlanPlaceholders.Lead);
    }

    [Test]
    public void Placeholders_WhenSprintDemo_ShouldAppearInTheTemplate()
    {
        var facts = _prompts.Facts(PromptKey.SprintDemo);
        var text = _prompts.Text(PromptKey.SprintDemo);

        facts.Placeholders.ShouldNotBeEmpty();
        facts.Placeholders.ShouldAllBe(placeholder => text.Contains(placeholder, StringComparison.Ordinal));
        facts.Placeholders.ShouldBe(Regex.Matches(text, @"\{\{[a-z_]+\}\}").Select(match => match.Value).Distinct().ToList());
        facts.InsertedParts.ShouldBe(["intro", "tools", "models", "level", "ffmpeg", "return", "start"]);
    }

    [Test]
    public void Hash_ShouldBeSha256OfTheText()
    {
        foreach (var facts in _prompts.All)
        {
            _prompts.Hash(facts.Key).ShouldBe(PromptHash.Of(_prompts.Text(facts.Key)));
        }
    }
}
