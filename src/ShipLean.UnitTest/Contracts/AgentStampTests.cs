using ShipLean.Contracts;

namespace ShipLean.UnitTest.Contracts;

[TestFixture]
public class AgentStampTests
{
    [Test]
    public void Of_WhenVerifiedWithHost_ShouldNameTheHost()
    {
        AgentStamp.Of(new AgentIdentity("Claude Code", "claude.ai", true)).ShouldBe("Claude Code (claude.ai)");
    }

    [TestCase(null, "claude.ai", true)]
    [TestCase("", "claude.ai", true)]
    [TestCase("   ", "claude.ai", true)]
    public void Of_WhenNameIsBlank_ShouldBeNull(string? name, string? host, bool verified)
    {
        AgentStamp.Of(new AgentIdentity(name, host, verified)).ShouldBeNull();
    }

    [TestCase("claude.ai", false)]
    [TestCase(null, true)]
    [TestCase("  ", true)]
    public void Of_WhenNotVerifiedOrHostBlank_ShouldSayUnverified(string? host, bool verified)
    {
        AgentStamp.Of(new AgentIdentity("Claude Code", host, verified)).ShouldBe("Claude Code (unverified)");
    }

    [Test]
    public void Of_WhenNoAgent_ShouldBeNull()
    {
        AgentStamp.Of(AgentIdentity.None).ShouldBeNull();
    }
}
