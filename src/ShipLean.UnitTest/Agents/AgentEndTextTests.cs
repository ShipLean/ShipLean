using Shouldly;
using ShipLean.Contracts.Agents;

namespace ShipLean.UnitTest.Agents;

// C2: the one sentence per end reason. A null detail drops ": <detail>"; agentError names only the first line.
[TestFixture]
public class AgentEndTextTests
{
    [TestCase(AgentEndReasons.DeliveryFailed, "No answer within 5 seconds. (2 tries)", "The agent connection didn't take the job: No answer within 5 seconds. (2 tries)")]
    [TestCase(AgentEndReasons.DeliveryFailed, null, "The agent connection didn't take the job.")]
    [TestCase(AgentEndReasons.AgentError, "Tests failed.", "The agent reported an error: Tests failed.")]
    [TestCase(AgentEndReasons.AgentError, null, "The agent reported an error.")]
    [TestCase(AgentEndReasons.Stale, "No activity for 30 minutes since 2026-11-16T01:00:00Z.", "The agent went quiet for 30 minutes.")]
    [TestCase(AgentEndReasons.Canceled, null, "The agent session was stopped.")]
    [TestCase(AgentEndReasons.Completed, null, "The agent finished.")]
    [TestCase(AgentEndReasons.ProposalWithoutIssue, "The item has no GitHub issue to propose on.", "The agent session ended: The item has no GitHub issue to propose on.")]
    [TestCase(AgentEndReasons.ProposalWithoutIssue, null, "The agent session ended.")]
    [TestCase(AgentEndReasons.Unknown, null, "The agent session ended before ShipLean recorded why.")]
    public void For_WhenAReason_ShouldGiveItsSentence(string reason, string? detail, string expected)
    {
        AgentEndText.For(reason, detail).ShouldBe(expected);
    }

    [Test]
    public void For_WhenAgentErrorHasSeveralLines_ShouldUseTheFirstLineOnly()
    {
        AgentEndText.For(AgentEndReasons.AgentError, "Build broke.\nat Foo.Bar()\nat Baz()").ShouldBe("The agent reported an error: Build broke.");
    }

    [Test]
    public void For_WhenDetailIsBlank_ShouldDropIt()
    {
        AgentEndText.For(AgentEndReasons.DeliveryFailed, "  ").ShouldBe("The agent connection didn't take the job.");
    }

    [TestCaseSource(typeof(AgentEndReasons), nameof(AgentEndReasons.All))]
    public void For_WhenAnyKnownReason_ShouldNotUseTheFallback(string reason)
    {
        AgentEndText.For(reason, "detail").ShouldNotContain($"({reason})");
    }
}
