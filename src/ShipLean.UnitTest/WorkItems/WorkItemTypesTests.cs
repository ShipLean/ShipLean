using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts.Refs;

namespace ShipLean.UnitTest.WorkItems;

[TestFixture]
public class WorkItemTypesTests
{
    [Test]
    public void Name_WhenAction_ShouldBeActionItem()
    {
        WorkItemTypeRules.Name(WorkItemTypeId.Action).ShouldBe("Action item");
        WorkItemTypes.Ref(WorkItemTypeId.Action).Name.ShouldBe("Action item");
        WorkItemTypeRules.Name(WorkItemTypeId.Story).ShouldBe("User Story");
        WorkItemTypeRules.Name(WorkItemTypeId.Bug).ShouldBe("Bug");
    }

    [TestCase(WorkItemTypeId.Epic, false)]
    [TestCase(WorkItemTypeId.Feature, false)]
    [TestCase(WorkItemTypeId.Story, true)]
    [TestCase(WorkItemTypeId.Bug, true)]
    [TestCase(WorkItemTypeId.Task, false)]
    [TestCase(WorkItemTypeId.Action, true)]
    public void IsStoryLevel_WhenEachType_ShouldMatchStoryBugAction(WorkItemTypeId type, bool expected)
    {
        WorkItemTypeRules.IsStoryLevel(type).ShouldBe(expected);
        WorkItemTypeRules.StoryLevelDb.Contains(WorkItemTypes.Db(type)).ShouldBe(expected);
    }

    [Test]
    public void IsAllowedParent_WhenAction_ShouldAllowFeatureOrNone()
    {
        WorkItemTypes.IsAllowedParent(WorkItemTypeId.Action, WorkItemTypeId.Feature).ShouldBeTrue();
        foreach (var parent in new[] { WorkItemTypeId.Epic, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task, WorkItemTypeId.Action })
        {
            WorkItemTypes.IsAllowedParent(WorkItemTypeId.Action, parent).ShouldBeFalse(parent.ToString());
        }
        WorkItemTypes.BoardLevel(WorkItemTypeId.Action).ShouldBe("stories");
    }

    [Test]
    public void IsAllowedParent_WhenTaskUnderAction_ShouldAllow()
    {
        WorkItemTypes.IsAllowedParent(WorkItemTypeId.Task, WorkItemTypeId.Action).ShouldBeTrue();
        WorkItemTypes.IsAllowedParent(WorkItemTypeId.Task, WorkItemTypeId.Story).ShouldBeTrue();
        WorkItemTypes.IsAllowedParent(WorkItemTypeId.Task, WorkItemTypeId.Feature).ShouldBeFalse();
    }
}
