using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using Shouldly;

namespace ShipLean.UnitTest.Agents;

// C3 as amended by D9: members see the end reason and its sentence; a deliveryFailed detail, every delivery attempt and every
// run log are for admins.
[TestFixture]
public class AgentSessionVisibilityTests
{
    private static readonly AgentRunLogDto RunLog = new(Guid.NewGuid(), "agent-run.log", 120, "/api/attachments/x");

    [Test]
    public void Apply_WhenMemberAndDeliveryFailed_ShouldHideDetailRunLogAndDeliveries()
    {
        var view = AgentSessionVisibility.Apply(AgentEndReasons.DeliveryFailed, "No answer from relay.internal:5100.", RunLog, ProjectRole.Member);

        view.EndReason.ShouldBe(AgentEndReasons.DeliveryFailed);
        view.EndDetail.ShouldBeNull();
        view.RunLog.ShouldBeNull();
        view.ShowDeliveries.ShouldBeFalse();
        view.EndText.ShouldBe(AgentEndText.For(AgentEndReasons.DeliveryFailed, null));
    }

    [Test]
    public void Apply_WhenMemberAndAgentError_ShouldShowTheDetailButNoRunLogOrDeliveries()
    {
        var view = AgentSessionVisibility.Apply(AgentEndReasons.AgentError, "The tests failed.", RunLog, ProjectRole.Member);

        view.EndDetail.ShouldBe("The tests failed.");
        view.RunLog.ShouldBeNull();
        view.ShowDeliveries.ShouldBeFalse();
        view.EndText.ShouldBe(AgentEndText.For(AgentEndReasons.AgentError, "The tests failed."));
    }

    [TestCaseSource(typeof(AgentEndReasons), nameof(AgentEndReasons.All))]
    public void Apply_WhenMember_ShouldNeverGetTheRunLog(string reason)
    {
        AgentSessionVisibility.Apply(reason, "detail", RunLog, ProjectRole.Member).RunLog.ShouldBeNull();
    }

    [TestCaseSource(typeof(AgentEndReasons), nameof(AgentEndReasons.All))]
    public void Apply_WhenMember_ShouldNeverGetDeliveries(string reason)
    {
        AgentSessionVisibility.Apply(reason, "detail", RunLog, ProjectRole.Member).ShowDeliveries.ShouldBeFalse();
    }

    [TestCaseSource(typeof(AgentEndReasons), nameof(AgentEndReasons.All))]
    public void Apply_WhenAdmin_ShouldGetEverything(string reason)
    {
        var view = AgentSessionVisibility.Apply(reason, "detail", RunLog, ProjectRole.Admin);

        view.ShouldBe(new AgentSessionEndView(reason, AgentEndText.For(reason, "detail"), "detail", RunLog, ShowDeliveries: true));
    }

    [TestCase(ProjectRole.Member, false)]
    [TestCase(ProjectRole.Admin, true)]
    public void Apply_WhenLive_ShouldHaveNoEndAndDeliveriesForAdminsOnly(ProjectRole role, bool showDeliveries)
    {
        AgentSessionVisibility.Apply(null, null, null, role).ShouldBe(new AgentSessionEndView(null, null, null, null, showDeliveries));
    }
}
