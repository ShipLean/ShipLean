using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;
using ShipLean.TestSupport.Identity;

namespace ShipLean.UnitTest.Contracts.WorkItems;

// The in-memory store labels its reads in the viewer's zone, as the real store does. The item changes at 20:00 UTC on Sun 10 Mar
// 2024 (Mon 11 Mar in Bangkok) and is read in June 2025, so the label is a day with its year.
[TestFixture]
public class InMemoryWorkItemViewerZoneTests
{
    private sealed class SteppedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static async Task<string> TheirChangeLabelAsync(IViewerZone zone)
    {
        var clock = new SteppedClock(new DateTimeOffset(2024, 3, 10, 20, 0, 0, TimeSpan.Zero));
        var store = InMemoryWorkItemArrangement.CreateStore(new PassThroughWorkItemRules(), WorkItemContractSupport.Actor, clock, zone);
        var project = InMemoryWorkItemArrangement.Arrange(store);
        var story = await WorkItemContractSupport.CreateAsync(store, project, WorkItemTypeId.Story, "Original");
        WorkItemContractSupport.Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Theirs") }), WriteOptions.User));
        clock.Now = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var outcome = await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Mine") }), WriteOptions.User);

        return outcome.Conflict.ShouldNotBeNull().TheirChanges.Single().ChangedAtLabel;
    }

    // Mutation: use TimeZoneInfo.Utc instead of viewerZone.Zone in InMemoryDtoBuilder.Revision.
    [Test]
    public async Task Patch_WhenViewerIsInBangkok_ShouldLabelTheirChangeByTheBangkokDay()
    {
        (await TheirChangeLabelAsync(new FixedViewerZone(ViewerZoneTestData.Bangkok))).ShouldBe("11 Mar 2024");
    }

    [Test]
    public async Task Patch_WhenViewerHasNoZone_ShouldLabelTheirChangeByTheUtcDay()
    {
        (await TheirChangeLabelAsync(new FixedViewerZone())).ShouldBe("10 Mar 2024");
    }
}
