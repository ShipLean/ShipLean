using System.Text.Json.Nodes;
using ShipLean.Contracts.Prompts;
using ShipLean.UnitTest.Dtos.Projects;

namespace ShipLean.UnitTest.Dtos.Prompts;

// C1: the pinned prompts fixtures survive a round trip through their DTOs, and obey the page's rules.
[TestFixture]
public class PromptsDtoPinnedTests
{
    private static JsonNode Read(string file) => ProjectDtoFixture.Read("prompts", file);

    [Test]
    public void Page_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ProjectDtoFixture.SurvivesRoundTrip<PromptsPageDto>(Read("prompts-page.json")).ShouldBeTrue();
    }

    [Test]
    public void History_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ProjectDtoFixture.SurvivesRoundTrip<PromptHistoryDto>(Read("prompt-history.json")).ShouldBeTrue();
    }

    [Test]
    public void Assembled_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ProjectDtoFixture.SurvivesRoundTrip<PromptAssembledDto>(Read("prompt-assembled.json")).ShouldBeTrue();
    }

    [Test]
    public void Saved_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ProjectDtoFixture.SurvivesRoundTrip<PromptSavedDto>(Read("prompt-saved.json")).ShouldBeTrue();
    }

    [Test]
    public void Page_WhenFixture_ShouldCountCustomsAndCarryUpdateOnlyWhenBuiltInUpdated()
    {
        var page = Read("prompts-page.json");
        var prompts = page["stages"]!.AsArray().SelectMany(stage => stage!["prompts"]!.AsArray()).Select(prompt => prompt!).ToList();

        prompts.Count.ShouldBe(11);
        page["stages"]!.AsArray().Count.ShouldBe(7);
        page["customCount"]!.GetValue<int>().ShouldBe(
            prompts.Count(prompt => prompt["status"]!.GetValue<string>() is "custom" or "builtInUpdated"));
        foreach (var prompt in prompts)
        {
            var status = prompt["status"]!.GetValue<string>();
            (prompt["update"] is not null).ShouldBe(status == "builtInUpdated", prompt["key"]!.GetValue<string>());
            if (status == "builtIn")
            {
                prompt["custom"].ShouldBeNull();
            }
        }

        prompts.Count(prompt => prompt["sectionErrors"]!.GetValue<int>() > 0).ShouldBe(1);
    }

    [Test]
    public void Page_WhenFixture_ShouldTakeEveryPatchFromThePatchCases()
    {
        var cases = Read("prompt-patches.json")["cases"]!.AsArray().Select(c => c!["patch"]!.GetValue<string>()).ToHashSet();
        var prompts = Read("prompts-page.json")["stages"]!.AsArray().SelectMany(stage => stage!["prompts"]!.AsArray()).Select(prompt => prompt!);

        foreach (var prompt in prompts)
        {
            if (prompt["yourChangesPatch"] is { } yours)
            {
                cases.ShouldContain(yours.GetValue<string>());
            }

            if (prompt["update"] is { } update)
            {
                cases.ShouldContain(update["patch"]!.GetValue<string>());
            }
        }
    }
}
