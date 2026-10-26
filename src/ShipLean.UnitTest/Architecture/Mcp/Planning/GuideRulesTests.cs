using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture.Mcp.Planning;

// Guard rails for the planning guide's text: it may name only tools and fields that exist, and no vendor or client.
// The self-tests run the same scans over fixture files that break each rule, so the rules can fail.
[TestFixture]
public class GuideRulesTests
{
    private static IReadOnlyDictionary<string, string> Fixture(string name)
        => new Dictionary<string, string> { [name] = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.UnitTest", "Architecture", "Mcp", "Planning", "Fixtures", name)) };

    [Test]
    public void Guide_ShouldNameOnlyRegisteredTools()
    {
        var files = GuideScan.GuideFiles();

        files.Count.ShouldBe(9);
        GuideScan.UnregisteredTokens(files, GuideScan.RegisteredNames()).ShouldBeEmpty("Write a name that is not a tool, prompt, parameter or field without backticks.");
    }

    [Test]
    public void Guard_WhenGuideNamesUnknownTool_ShouldFail()
    {
        GuideScan.UnregisteredTokens(Fixture("unknown-tool.md"), GuideScan.RegisteredNames())
            .ShouldBe(["unknown-tool.md: `not_a_tool`"]);
    }

    [Test]
    public void Guide_ShouldNameNoVendorOrClient()
    {
        var files = GuideScan.GuideFiles();

        files.Count.ShouldBe(9);
        GuideScan.BannedMentions(files).ShouldBeEmpty("Use roles: the strongest model, a fast cheap model, a reviewer from a different model family.");
    }

    [Test]
    public void Guard_WhenGuideNamesAVendor_ShouldFail()
    {
        GuideScan.BannedMentions(Fixture("vendor.md"))
            .ShouldBe(["vendor.md: AskQuestion", "vendor.md: Claude", "vendor.md: Composer", "vendor.md: Gemini", "vendor.md: Task tool", "vendor.md: subagent_type", "vendor.md: ~/.cursor"]);
    }

    [Test]
    public void Guard_WhenGuideNamesTheGuideParts_ShouldPass()
    {
        var files = new Dictionary<string, string> { ["parts.md"] = "Ask for `plan_reviewer` or `code_reviewer`, then `retro`." };

        GuideScan.RegisteredNames().ShouldContain("plan_reviewer");
        GuideScan.UnregisteredTokens(files, GuideScan.RegisteredNames()).ShouldBeEmpty();
    }

    [Test]
    public void Guard_WhenGuideUsesOrdinaryWords_ShouldNotTripTheVendorGuard()
    {
        var plain = new Dictionary<string, string> { ["plain.md"] = "Page with the `cursor` field. A magnum opus, or a task tool of your own." };
        var named = new Dictionary<string, string> { ["named.md"] = "Open it in Cursor." };

        GuideScan.BannedMentions(plain).ShouldBeEmpty();
        GuideScan.BannedMentions(named).ShouldBe(["named.md: Cursor"]);
    }
}
