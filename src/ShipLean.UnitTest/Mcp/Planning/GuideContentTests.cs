using ShipLean.WebApi.Features.Mcp.Planning;

namespace ShipLean.UnitTest.Mcp.Planning;

[TestFixture]
public class GuideContentTests
{
    private static string Guide => PlanGuide.Read(PlanGuidePart.Guide);
    private static string Work => PlanGuide.Read(PlanGuidePart.Work);

    [Test]
    public void WorkContent_ShouldNameIdeaToDoneTools()
    {
        foreach (var tool in new[] { "search", "create_feature", "attach_markdown", "comment", "set_status", "read_attachment", "get_work_item", "planning_guide", "plan_feature", "capture_plan" })
        {
            Work.ShouldContain($"`{tool}`");
        }
    }

    [Test]
    public void GuideContent_ShouldStateTheFourConstraints()
    {
        Guide.ShouldContain("## The four constraints");
        Guide.ShouldContain("**Context window.**");
        Guide.ShouldContain("**No dependencies between agents running in parallel.**");
        Guide.ShouldContain("**Design for flow, not for waits.**");
        Guide.ShouldContain("**Hand off only at contracts, and Agent 0 builds them first.**");
    }

    [Test]
    public void GuideContent_ShouldMapModesToStories()
    {
        Guide.ShouldContain("| Direct |");
        Guide.ShouldContain("One Story. No leads");
        Guide.ShouldContain("One Story per wave.");
        Guide.ShouldContain("One Story per lane.");
    }

    [Test]
    public void GuideContent_ShouldNameEveryPlanningTool()
    {
        foreach (var tool in new[] { "planning_guide", "capture_plan", "get_brief", "report_task", "plan_status" })
        {
            Guide.ShouldContain($"`{tool}`");
        }
    }

    [Test]
    public void GuideContent_LeadTemplate_ShouldPickUpTheStoryAndCloseIt()
    {
        var lead = PlanGuide.Read(PlanGuidePart.Lead);

        lead.ShouldContain("`pick_up` on your story");
        lead.ShouldContain("`get_brief` on each of your tasks");
        lead.ShouldContain("close your story with `update_progress`");
    }

    [Test]
    public void GuideContent_ShouldAttachTheRetroAndSetTheStatus()
    {
        Guide.ShouldContain("`attach_markdown`");
        Guide.ShouldContain("retro-<yyyy-mm-dd>-<topic>.md");
        Guide.ShouldContain("`set_status`");
        Guide.ShouldContain("## Contracts");
    }

    [Test]
    public void GuideContent_ShouldReviewThePlanBetweenPreviewAndApply()
    {
        Guide.ShouldContain("exactly the same arguments plus that `plan`");
        Guide.ShouldContain("there is no second capture");
        Guide.IndexOf("Have a reviewer", StringComparison.Ordinal)
            .ShouldBeLessThan(Guide.IndexOf("Apply by calling `capture_plan`", StringComparison.Ordinal));
    }

    [Test]
    public void GuideContent_ShouldCloseEveryStoryWithUpdateProgress()
    {
        Guide.ShouldContain("**Close the Story.**");
        Guide.ShouldContain("the conductor for Agent 0's Story");
        Guide.ShouldContain("a direct-mode agent");
        Guide.ShouldContain("Direct mode still captures its one Story");
    }

    [Test]
    public void GuideContent_FixTemplate_ShouldReadTheReviewWithGetWorkItem()
    {
        var fix = PlanGuide.Read(PlanGuidePart.Fix);

        fix.ShouldContain("`get_work_item`");
        fix.ShouldNotContain("`get_brief`");
    }
}
