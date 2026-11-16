using System.Text;
using System.Text.RegularExpressions;
using ShipLean.Application.Features.Demos.Prompt;
using ShipLean.Contracts.Demos;

namespace ShipLean.UnitTest.Demos;

// C2 and C4: an agent-relay demo session runs Claude Code in dontAsk with Bash allowed only for DemoRunnerTools.Commands, and refuses any command
// holding a $ expansion, a backquote or a ~ path. These tests read every command the rendered session prompt tells the agent to run.
[TestFixture]
public partial class SprintDemoPromptCommandsTests
{
    private const string UploadUrl = "https://sl.example/api/uploads/demo?token=UPLOADSENTINEL";
    private const string LogoUrl = "https://sl.example/api/downloads/demo-logo?token=LOGOSENTINEL";

    private const string EnvironmentRule =
        "Start it, wait until it answers and record against it, but only if every command it needs is one you are allowed to run (step 6). "
        + "Otherwise build the demo from slides alone and say so in the intro and in your final response.";

    // Printed as text only; these tests never read it.
    private static readonly DateTimeOffset Expires = DateTimeOffset.UnixEpoch;

    private static readonly DemoBrand DefaultBrand = new(true, "<svg id=\"default-logo\"/>", null, null, null, true, ":root { --color-bg: #e8eef7; }", null);

    private static readonly DemoBrand ProjectBrand = new(false, null, LogoUrl, "image/png", Expires, false, ":root { --color-bg: #e8eef7; }", null);

    private static string Render(string environment = "Run the app with make dev.", DemoBrand? brand = null, string uploadUrl = UploadUrl, DemoPromptChannel channel = DemoPromptChannel.Session)
        => new SprintDemoPrompt().Render(Data(environment), new DemoUploadTarget(uploadUrl, Expires, 200L * 1024 * 1024, "BAU-7", "Sprint 24"), brand ?? DefaultBrand, channel);

    private static SprintDemoData Data(string environment)
        => new(
            new SprintDemoSprint("Sprint 24", "Coupons v2", new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 2), true),
            new SprintDemoProject("KEY", "Shop", "https://example.com/shop", "main", environment),
            [new SprintDemoFeatureData("KEY-1", "Checkout v2", "Done", "/p/KEY/items/KEY-1", "One-step checkout", "Shoppers drop out.", "Completion rate", [],
                new SprintDemoPlanFacts(2, 3, 9, 2, []), new SprintDemoRetroData([], [], [], []), [], [])],
            [],
            new SprintDemoRetroData([], [], [], []),
            [],
            false);

    // The three session renders: with an environment, with none, and with a project logo (whose fetch is a curl command).
    private static IEnumerable<TestCaseData> Sessions()
    {
        yield return new TestCaseData(Render()).SetName("{m}_WithAnEnvironment");
        yield return new TestCaseData(Render("  ")).SetName("{m}_WithNoEnvironment");
        yield return new TestCaseData(Render(brand: ProjectBrand)).SetName("{m}_WithAProjectLogo");
    }

    // Mutation: put $FF back in one step-11 command, or a pip install line in step 6.
    [TestCaseSource(nameof(Sessions))]
    public void SessionPrompt_ShouldOnlyRunAllowedCommands(string prompt)
    {
        var commands = ShellFences(prompt).SelectMany(Commands).Concat(InlineCommands(prompt)).ToList();

        commands.ShouldNotBeEmpty();
        commands.Where(c => !DemoRunnerTools.Commands.Contains(FirstWord(c))).ShouldBeEmpty();
        commands.ShouldContain(c => c.StartsWith("demo-python --version", StringComparison.Ordinal));
        commands.ShouldContain(c => c.StartsWith("ffprobe -version", StringComparison.Ordinal));
        commands.ShouldContain(c => c.StartsWith("curl --fail-with-body", StringComparison.Ordinal));
    }

    // The environment block's text fence is project data and may hold any command: the session prompt only lets the agent run it when every
    // command is allowed, so test 1 skips it (it is a text fence, not a shell fence) and pins that sentence instead.
    [Test]
    public void SessionPrompt_WhenAnEnvironmentIsSet_ShouldStartItOnlyWithAllowedCommands()
    {
        var text = Render("sudo docker compose up -d && $HOME/bin/seed");

        text.ShouldContain("```text\nsudo docker compose up -d && $HOME/bin/seed\n```\n\n" + EnvironmentRule);
        ShellFences(text).ShouldNotContain(f => f.Contains("docker", StringComparison.Ordinal));
        Render(channel: DemoPromptChannel.Handover).ShouldNotContain(EnvironmentRule);
    }

    // Mutation: "${f%.wav}-n.wav" in step 7.
    [TestCaseSource(nameof(Sessions))]
    public void SessionPrompt_ShouldHaveNoShellExpansion(string prompt)
    {
        foreach (var fence in ShellFences(prompt))
        {
            fence.ShouldNotContain("$");
            fence.ShouldNotContain("`");
            fence.ShouldNotContain("~");
        }
    }

    // A base URL holding every character dontAsk refuses, or one that would end the single-quoted word, is percent-encoded in each shell fence.
    [Test]
    public void SessionPrompt_WhenTheBaseUrlIsHostile_ShouldHaveNoShellExpansion()
    {
        const string hostile = "https://sl.example/a'b$c`d~e f";
        var brand = ProjectBrand with { LogoUrl = hostile + "/api/downloads/demo-logo?token=LOGOSENTINEL" };

        var text = Render(brand: brand, uploadUrl: hostile + "/api/uploads/demo?token=UPLOADSENTINEL");

        var fences = ShellFences(text);
        fences.ShouldContain(f => f.Contains("'https://sl.example/a%27b%24c%60d%7Ee%20f/api/uploads/demo?token=UPLOADSENTINEL'", StringComparison.Ordinal));
        fences.ShouldContain(f => f.Contains("'https://sl.example/a%27b%24c%60d%7Ee%20f/api/downloads/demo-logo?token=LOGOSENTINEL'", StringComparison.Ordinal));
        foreach (var fence in fences)
        {
            fence.ShouldNotContain("$");
            fence.ShouldNotContain("`");
            fence.ShouldNotContain("~");
        }
    }

    // Mutation: keep the handover return block for the session.
    [Test]
    public void SessionPrompt_ShouldReturnThroughFinalResponse()
    {
        var text = Render();

        text.ShouldContain("agent-relay posts your final response as a comment on `BAU-7`");
        text.ShouldContain("end your run with your closing comment as your final response, which is posted on `BAU-7`");
        text.ShouldContain("If a check fails, end your run with the exact failure as your final response.");
        text.ShouldNotContain("`comment` on");
        text.ShouldNotContain("log_retrospective");
        text.ShouldNotContain("gh pr");
        text.ShouldNotContain("git push");
        text.ShouldNotContain("git commit");
        text.ShouldNotContain("~/.local/demo-tools");
        text.ShouldNotContain("pip install");
        text.ShouldNotContain("playwright install");
    }

    // Mutation: expanduser("~/.local/demo-tools").
    [Test]
    public void SessionPrompt_ShouldReadModelsFromDemoTools()
    {
        var python = Fences(Render()).Single(f => f.Language == "python").Body;

        python.ShouldContain("D = os.environ[\"DEMO_TOOLS\"]");
        python.ShouldNotContain("expanduser");
        Render().ShouldContain("run it with `demo-python narrate.py`");
    }

    // Amendment (16 Nov): relay activities show the agent's own messages, so the link must never appear in them or in the final response.
    // Mutation: drop the sentence from the session return block.
    [Test]
    public void SessionPrompt_ShouldNeverWriteTheUploadLinkInMessagesOrTheFinalResponse()
    {
        var text = Render();

        text.ShouldContain("The upload link is used only inside that `curl` command. **Never write the upload link, or any part of its query or token, "
                           + "in the messages you write as you work or in your final response**, not even when the upload fails");
        text.IndexOf("Never write the upload link, or any part of its query or token", StringComparison.Ordinal)
            .ShouldBeGreaterThan(text.IndexOf("## 13. Return", StringComparison.Ordinal));
    }

    private sealed record Fence(string Language, string Body);

    private static List<Fence> Fences(string text)
        => [.. FenceBlock().Matches(text).Select(m => new Fence(m.Groups["lang"].Value, m.Groups["body"].Value))];

    private static List<string> ShellFences(string text) => [.. Fences(text).Where(f => f.Language is "bash" or "sh").Select(f => f.Body)];

    // Text outside every fence, so data and code in fences never reads as an inline command.
    private static string Prose(string text) => FenceBlock().Replace(text, string.Empty);

    // An inline code span is a command when it is a lowercase word followed by a flag, a file name or another bare word (`ls -l demo.mp4`,
    // `demo-python narrate.py`, `pip install x`). Spans such as `file 'seg-s01.mp4'` (a line of list.txt) or `subprocess.run([...])` are not.
    private static IEnumerable<string> InlineCommands(string text)
        => InlineSpan().Matches(Prose(text)).Select(m => m.Groups[1].Value).Where(span => InlineCommand().IsMatch(span));

    // Splits a fence into commands: comment lines dropped, backslash continuations joined, then split outside quotes on &&, ||, ;, | and
    // newlines, with a leading do or then removed.
    private static IEnumerable<string> Commands(string fence)
    {
        var lines = fence.Split('\n').Where(l => !l.TrimStart().StartsWith('#'));
        var joined = string.Join('\n', lines).Replace("\\\n", " ", StringComparison.Ordinal);
        var commands = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var i = 0;
        while (i < joined.Length)
        {
            var c = joined[i];
            if (quote != '\0')
            {
                quote = c == quote ? '\0' : quote;
                current.Append(c);
            }
            else if (c is '\'' or '"')
            {
                quote = c;
                current.Append(c);
            }
            else if (c is ';' or '|' or '\n' || (c == '&' && i + 1 < joined.Length && joined[i + 1] == '&'))
            {
                commands.Add(current.ToString());
                current.Clear();
                i += (c is '&' or '|') && i + 1 < joined.Length && joined[i + 1] == c ? 1 : 0;
            }
            else
            {
                current.Append(c);
            }
            i++;
        }
        commands.Add(current.ToString());
        return commands.Select(c => KeywordPrefix().Replace(c.Trim(), string.Empty)).Where(c => c.Length > 0);
    }

    private static string FirstWord(string command) => command.Split(' ', 2)[0];

    [GeneratedRegex(@"^(?<fence>`{3,})(?<lang>[a-z]*)\n(?<body>.*?)\n\k<fence>$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FenceBlock();

    [GeneratedRegex("`([^`\n]+)`")]
    private static partial Regex InlineSpan();

    [GeneratedRegex(@"^[a-z][a-z0-9-]* (-|[\w.-]+\.[a-z0-9]+(\s|$)|[a-z]+(\s|$))")]
    private static partial Regex InlineCommand();

    [GeneratedRegex(@"^(do|then)\s+")]
    private static partial Regex KeywordPrefix();
}
