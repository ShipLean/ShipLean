using ShipLean.Application.Features.Inspection.Views;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// The attribute keys that hold content, pinned: adding to this list is a decision, taking from it leaks prompts and tool output.
[TestFixture]
public class ContentKeysTests
{
    [Test]
    public void Hidden_ShouldBeExactlyTheKeysThatCarryPromptsAndToolIo()
    {
        ContentKeys.Hidden.Order(StringComparer.Ordinal).ShouldBe(
            ["command", "content", "input", "output", "prompt", "prompt_text", "response", "result", "tool_input", "tool_output", "tool_parameters"]);
    }

    [Test]
    public void Visible_WhenContentIsHidden_ShouldKeepOnlyTheOtherKeysWhateverTheirCase()
    {
        var attributes = new Dictionary<string, string> { ["Prompt"] = "p", ["tool_parameters"] = "x", ["tool_name"] = "Edit", ["prompt.id"] = "p-1", ["duration_ms"] = "5" };

        ContentKeys.Visible(attributes, false).Select(a => a.Key).Order(StringComparer.Ordinal).ShouldBe(["duration_ms", "prompt.id", "tool_name"]);
        ContentKeys.Visible(attributes, true).Count().ShouldBe(5);
    }
}
