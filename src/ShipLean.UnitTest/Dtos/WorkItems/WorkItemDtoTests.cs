using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.UnitTest.Dtos.WorkItems;

[TestFixture]
public class WorkItemDtoTests
{
    [TestCase("work-item.story.json")]
    [TestCase("work-item.bug.json")]
    [TestCase("work-item.feature.json")]
    public void WorkItemDto_WhenRoundTripped_ShouldMatchTheFixture(string file)
    {
        WorkItemDtoFixture.SurvivesRoundTrip<WorkItemDto>(WorkItemDtoFixture.Read(file)).ShouldBeTrue();
    }

    [Test]
    public void ConflictDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        WorkItemDtoFixture.SurvivesRoundTrip<ConflictDto>(WorkItemDtoFixture.Read("conflict-history.json")["conflict"]!).ShouldBeTrue();
    }

    [Test]
    public void HistoryDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var history = WorkItemDtoFixture.Read("conflict-history.json")["history"]!;
        WorkItemDtoFixture.SurvivesRoundTrip<HistoryDto>(history).ShouldBeTrue();
        history["revisions"]!.AsArray().Select(r => r!["rev"]!.GetValue<int>()).ShouldBe([6, 5, 4, 3, 2, 1]);
    }

    [Test]
    public void RevisionDto_ShouldRoundTripAgentName()
    {
        var history = WorkItemDtoFixture.Read("conflict-history.json")["history"]!;
        WorkItemDtoFixture.SurvivesRoundTrip<HistoryDto>(history).ShouldBeTrue();

        var revisions = history.Deserialize<HistoryDto>(WorkItemDtoFixture.Options)!.Revisions;
        revisions.Select(r => r.AgentName).Distinct().ShouldBe(["Claude Code (claude.ai)", null], ignoreOrder: true);
        JsonSerializer.SerializeToNode(revisions[0], WorkItemDtoFixture.Options)!["agentName"]!.GetValue<string>().ShouldBe("Claude Code (claude.ai)");
    }

    [Test]
    public void WorkItemChanges_WhenDeserialised_ShouldTellUnchangedFromCleared()
    {
        var changes = JsonSerializer.Deserialize<WorkItemChanges>(
            """{"points":{"value":null},"title":{"value":"New"},"removeLinkIds":[]}""", WorkItemDtoFixture.Options)!;

        changes.Points.ShouldNotBeNull().Value.ShouldBeNull();
        changes.Title.ShouldNotBeNull().Value.ShouldBe("New");
        changes.Priority.ShouldBeNull();
        changes.AddLinks.ShouldBeEmpty();
    }
}
