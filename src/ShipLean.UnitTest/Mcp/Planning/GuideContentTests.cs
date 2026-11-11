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
    public void GuideContent_ShouldCoverEveryBreakdownWhateverItsSize()
    {
        Guide.ShouldContain("Use this for every breakdown of a Feature or Epic into stories and tasks, whatever its size");
        Guide.ShouldNotContain("too big for one agent");
    }

    [Test]
    public void GuideContent_ShouldOfferACostlyCiVariantThatPushesOnce()
    {
        Guide.ShouldContain("**Costly CI.**");
        Guide.ShouldContain("which push variant of Step 6 applies");
        PlanGuide.Read(PlanGuidePart.Brief).ShouldContain("costly CI variant");
        PlanGuide.Read(PlanGuidePart.Fix).ShouldContain("costly CI variant");
    }

    [Test]
    public void GuideContent_ShouldSayWhatToDoWithoutAReviewerFromAnotherFamily()
    {
        Guide.ShouldContain("If no model from another family is available");
    }

    [Test]
    public void GuideContent_ShouldLetASmallPlanReuseRecentTestCommands()
    {
        Guide.ShouldContain("**Test commands.**");
        Guide.ShouldContain("may be reused without a fresh run");
    }

    [Test]
    public void GuideContent_FixTemplate_ShouldReadTheReviewWithGetWorkItem()
    {
        var fix = PlanGuide.Read(PlanGuidePart.Fix);

        fix.ShouldContain("`get_work_item`");
        fix.ShouldNotContain("`get_brief`");
    }

    [Test]
    public void GuideContent_ShouldOpenWithTheLearningFromFailurePreamble()
    {
        const string first = "Your primary goal is absolute truth over artificial success.";

        Guide.ShouldContain(first);
        Work.ShouldContain(first);
    }

    [Test]
    public void GuideContent_ShouldTellAgentsToLogRetrospectives()
    {
        Guide.ShouldContain("`log_retrospective`");
        Work.ShouldContain("`log_retrospective`");
        PlanGuide.Read(PlanGuidePart.Report).ShouldContain("`log_retrospective`");
        PlanGuide.Read(PlanGuidePart.Lead).ShouldContain("`log_retrospective`");
    }

    [Test]
    public void GuideContent_StepNine_ShouldCompileTheFeatureRetroWithCompiledFrom()
    {
        var stepNine = Guide[Guide.IndexOf("## Step 9", StringComparison.Ordinal)..];

        stepNine.ShouldContain("`compiled_from`");
        stepNine.ShouldContain("blameless site reliability engineer");
    }

    [Test]
    public void GuideContent_StepNine_ShouldDecideWhatToDoAfterCompiling()
    {
        var stepNine = Guide[Guide.IndexOf("## Step 9", StringComparison.Ordinal)..];

        var decide = stepNine.IndexOf("**Decide what to do.**", StringComparison.Ordinal);
        decide.ShouldBeGreaterThan(stepNine.IndexOf("`compiled_from`", StringComparison.Ordinal));
        stepNine[decide..].ShouldContain("`list_retrospectives`");
        stepNine[decide..].ShouldContain("`undecided`");
        stepNine[decide..].ShouldContain("`decide_retro_actions`");
        stepNine[decide..].ShouldContain("`not_needed`");
    }

    [Test]
    public void GuideContent_ShouldSayDecideIsNotAutomatic()
    {
        var stepNine = Guide[Guide.IndexOf("## Step 9", StringComparison.Ordinal)..];
        var decide = stepNine[stepNine.IndexOf("**Decide what to do.**", StringComparison.Ordinal)..];

        decide.ShouldContain("at most 3 new");
        decide.ShouldContain("no sprint");
        decide.ShouldContain("human approval");
        decide.ShouldContain("not automatic");
    }

    [Test]
    public void WorkContent_ShouldNameDecideRetroActions()
    {
        Work.ShouldContain("`decide_retro_actions`");
    }

    [Test]
    public void GuideContent_ShouldAskForAWhySection()
    {
        foreach (var text in new[] { Guide, Work })
        {
            text.ShouldContain("`## Why` section");
            text.ShouldContain("Problem, Who, We believe and Measure");
            text.ShouldContain("to confirm");
            text.ShouldContain("`create_sprint_demo`");
        }
    }

    [Test]
    public void GuideContent_WhenWorkPart_ShouldTellAgentsToRecordDecisions()
    {
        Work.ShouldContain("`record_decisions`");
        Work.ShouldContain("`ask`");
        Work.ShouldNotContain("Key facts and decisions");
    }

    [Test]
    public void GuideContent_WhenGuidePart_ShouldTellAgentsToAskOnTheItem()
    {
        Guide.ShouldContain("`ask`");
        Guide.ShouldContain("`record_decisions`");
        Guide.ShouldNotContain("the agent is waiting");
        Guide.ShouldNotContain("reply to the agent");
    }
}
