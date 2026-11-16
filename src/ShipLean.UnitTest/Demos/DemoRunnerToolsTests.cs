using ShipLean.Application.Features.Demos.Prompt;

namespace ShipLean.UnitTest.Demos;

// C2 mirror: the same command names, in the same order, as the Bash entries of agent-relay's DEMO_ALLOWED_TOOLS
// (pinned there by DemoArgumentsTests).
public sealed class DemoRunnerToolsTests
{
    [Test]
    public void Commands_ShouldMatchRelayDemoAllowList()
    {
        string.Join(",", DemoRunnerTools.Commands).ShouldBe("demo-python,ffmpeg,ffprobe,curl,mkdir,ls");
    }
}
