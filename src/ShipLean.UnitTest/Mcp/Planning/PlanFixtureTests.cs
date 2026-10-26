using System.Text;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.TestSupport.Plans;

namespace ShipLean.UnitTest.Mcp.Planning;

[TestFixture]
public class PlanFixtureTests
{
    private static PlanFixture Plan => PlanFixture.FeatureEditor;

    [Test]
    public void PlanFixture_ShouldLoadSixStoriesAndTwentyThreeTasks()
    {
        Plan.Stories.Count.ShouldBe(6);
        Plan.Stories.Sum(story => story.Tasks.Count).ShouldBe(23);
        Plan.ItemCount.ShouldBe(30);
        Plan.Stories.Select(story => (story.Wave.Text, story.Lane)).ShouldBe([("0.1", null), ("0.2", null), ("1", "F"), ("1", "A"), ("1", "L"), ("2", null)]);
        Plan.Stories.Single(story => story.Lane == "F").Tasks.Select(task => task.Title[..2]).ShouldBe(["F1", "F2", "F3", "F4", "F5"]);
        Plan.SharedBranch.ShouldBe("feat/2-feature-editor");
        MarkdownSections.Extract(Plan.Feature.Description, PlanHeadings.Contracts).ShouldNotBeNullOrWhiteSpace();
        Plan.CommonBrief.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void PlanFixture_ShouldSatisfyEveryPlanShapeRule()
    {
        foreach (var wave in Plan.Stories.GroupBy(story => story.Wave))
        {
            if (wave.Any(story => story.Lane is null))
            {
                wave.Count().ShouldBe(1, $"wave {wave.Key} has no lanes, so it is one story");
            }
            wave.Select(story => story.Lane).ShouldBeUnique();
        }
        Plan.Stories.Select(story => story.Wave).ShouldBe(Plan.Stories.Select(story => story.Wave).Order());
        Plan.ItemCount.ShouldBeLessThanOrEqualTo(60);
        Encoding.UTF8.GetByteCount(Plan.CommonBrief).ShouldBeLessThanOrEqualTo(64 * 1024);
        foreach (var task in Plan.Stories.SelectMany(story => story.Tasks))
        {
            task.Brief.ShouldNotBeNullOrWhiteSpace();
            Encoding.UTF8.GetByteCount(task.Brief).ShouldBeLessThanOrEqualTo(10 * 1024);
            task.ContextK.ShouldBeInRange(1, 100);
            task.Model.ShouldBeOneOf("strong", "fast");
        }
    }

    [Test]
    public void PlanFixture_Tags_ShouldFollowThePlacementRule()
    {
        Plan.FeatureTags.ShouldBe(new PlanTaskTags(Branch: "feat/2-feature-editor"));
        Plan.Feature.Description.ShouldNotContain("## Goal");
        foreach (var story in Plan.Stories)
        {
            story.Tags.ShouldBe(new PlanTaskTags(story.Wave, story.Lane));
            foreach (var task in story.Tasks)
            {
                task.Tags.ShouldBe(new PlanTaskTags(story.Wave, story.Lane, task.Model, task.ContextK));
            }
        }
        PlanTags.Format(Plan.Stories.Single(story => story.Lane == "F").Tasks[0].Tags).Take(2).ShouldBe(["wave:1", "lane:F"]);
    }

    [Test]
    public void Arguments_ShouldBeAFreshCopyOfTheCapturePlanArguments()
    {
        var first = Plan.Arguments();
        first.Remove("stories");

        var second = Plan.Arguments();

        second.Select(property => property.Key).ShouldBe(["feature", "shared_branch", "common_brief", "plan_markdown", "stories"]);
        second["stories"]!.AsArray().Count.ShouldBe(6);
        ((string?)second["stories"]![2]!["tasks"]![0]!["title"]).ShouldBe(Plan.Stories[2].Tasks[0].Title);
    }
}
