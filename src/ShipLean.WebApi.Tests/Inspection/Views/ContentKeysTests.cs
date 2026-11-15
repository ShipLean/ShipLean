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
            ["arguments", "command", "content", "input", "output", "prompt", "prompt_text", "response", "result", "tool_input", "tool_output", "tool_parameters"]);
    }

    [Test]
    public void Visible_WhenContentIsHidden_ShouldKeepOnlyTheOtherKeysWhateverTheirCase()
    {
        var attributes = new Dictionary<string, string> { ["Prompt"] = "p", ["tool_parameters"] = "x", ["tool_name"] = "Edit", ["prompt.id"] = "p-1", ["duration_ms"] = "5" };

        ContentKeys.Visible(attributes, false).Select(a => a.Key).Order(StringComparer.Ordinal).ShouldBe(["duration_ms", "prompt.id", "tool_name"]);
        ContentKeys.Visible(attributes, true).Count().ShouldBe(5);
    }

    [Test]
    public void Visible_WhenContentIsHiddenAndTheKeyIsCodexToolInput_ShouldDropArguments()
    {
        var attributes = new Dictionary<string, string> { ["arguments"] = """{"cmd":"rm -rf x"}""", ["tool_name"] = "shell", ["call_id"] = "c1" };

        ContentKeys.Visible(attributes, false).Select(a => a.Key).Order(StringComparer.Ordinal).ShouldBe(["call_id", "tool_name"]);
    }

    [Test]
    public void Visible_WhenContentIsHiddenAndTheKeyIsDotted_ShouldMatchItsLastSegmentAndGenAiContent()
    {
        var attributes = new Dictionary<string, string>
        {
            ["foo.prompt"] = "p", ["tool.input"] = "i", ["gen_ai.prompt.0.content"] = "c", ["gen_ai.completion.0.content"] = "c", ["gen_ai.input.messages"] = "m",
            ["gen_ai.output.messages"] = "m", ["gen_ai.system_instructions"] = "s", ["gen_ai.tool.call.arguments"] = "a", ["codex.prompt"] = "p",
            ["gen_ai.usage.input_tokens"] = "10", ["gen_ai.request.model"] = "gpt", ["prompt.id"] = "p-1", ["tool.name"] = "Edit",
        };

        ContentKeys.Visible(attributes, false).Select(a => a.Key).Order(StringComparer.Ordinal)
            .ShouldBe(["gen_ai.request.model", "gen_ai.usage.input_tokens", "prompt.id", "tool.name"]);
    }
}
