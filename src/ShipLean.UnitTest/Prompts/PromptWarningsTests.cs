using ShipLean.Application.Features.Prompts.Writes;
using ShipLean.Contracts.Prompts;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.UnitTest.Prompts;

// The editor's placeholder and unknown-tag warnings, ported for save_prompt: same order and wording as promptChecks.ts.
[TestFixture]
public class PromptWarningsTests
{
    private static readonly string[] Placeholders = ["{{task_key}}", "{{task_brief}}"];

    [Test]
    public void Of_WhenAPlaceholderIsMissing_ShouldWarnInTheFactsOrder()
    {
        var warnings = PromptWarnings.Of("Nothing here.", Placeholders);

        warnings.ShouldBe([
            new PromptWarningDto(PromptWarningKind.PlaceholderMissing, "{{task_key}} is missing, so agents won't get that part.", "{{task_key}}", null, null),
            new PromptWarningDto(PromptWarningKind.PlaceholderMissing, "{{task_brief}} is missing, so agents won't get that part.", "{{task_brief}}", null, null),
        ]);
    }

    [Test]
    public void Of_WhenATokenIsUnknown_ShouldWarnOnceInFirstSeenOrder()
    {
        var warnings = PromptWarnings.Of("{{task_key}} {{task_brief}} {{zeta}} {{alpha}} {{zeta}}", Placeholders);

        warnings.ShouldBe([
            new PromptWarningDto(PromptWarningKind.PlaceholderUnknown, "{{zeta}} isn't one ShipLean fills in for this prompt; it stays as typed.", "{{zeta}}", null, null),
            new PromptWarningDto(PromptWarningKind.PlaceholderUnknown, "{{alpha}} isn't one ShipLean fills in for this prompt; it stays as typed.", "{{alpha}}", null, null),
        ]);
    }

    [Test]
    public void Of_WhenATagIsUnknown_ShouldWarnWithItsLine()
    {
        var warnings = PromptWarnings.Of("{{task_key}}\r\n{{task_brief}}\r\n<claude-cod>\r\nOnly some.\r\n</claude-cod>", Placeholders);

        warnings.Select(w => (w.Kind, w.Tag, w.Line)).ShouldBe([(PromptWarningKind.UnknownTag, "claude-cod", 3), (PromptWarningKind.UnknownTag, "claude-cod", 5)]);
        warnings[0].Message.ShouldBe("<claude-cod> isn't an agent ShipLean recognises, so every agent gets it as typed.");
    }

    [Test]
    public void Of_WhenMissingUnknownAndTagTogether_ShouldListMissingThenUnknownThenTags()
    {
        var warnings = PromptWarnings.Of("<claude-cod>\n{{other}}\n{{task_brief}}", Placeholders);

        warnings.Select(w => w.Kind).ShouldBe([PromptWarningKind.PlaceholderMissing, PromptWarningKind.PlaceholderUnknown, PromptWarningKind.UnknownTag]);
    }

    [Test]
    public void Of_WhenTheBuiltInTextOfEveryKey_ShouldWarnNothing()
    {
        var builtIns = new BuiltInPrompts();

        foreach (var key in Enum.GetValues<PromptKey>())
        {
            PromptWarnings.Of(builtIns.Text(key), builtIns.Facts(key).Placeholders).ShouldBeEmpty(key.ToString());
        }
    }
}
