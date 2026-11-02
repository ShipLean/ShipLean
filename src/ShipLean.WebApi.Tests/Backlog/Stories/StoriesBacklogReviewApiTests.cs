using ShipLean.Contracts.Backlog;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// The review field of the Stories backlog read: all five checks in list and planning mode, nothing in tree mode.
[TestFixture]
public class StoriesBacklogReviewApiTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog";

    [TestCase("list")]
    [TestCase("planning")]
    public async Task Get_WhenListOrPlanning_ShouldCarryFiveChecks(string mode)
    {
        var view = await Api.GetBacklogAsync($"{Url}?mode={mode}");

        var review = view.Review.ShouldNotBeNull();
        review.Top.ShouldBe(BulkLimits.MaxRows);
        review.StaleDays.ShouldBe(30);
        review.Checks.Select(c => c.Id).ShouldBe(BacklogCheckIds.All);
    }

    [Test]
    public async Task Get_WhenTree_ShouldHaveNoReview()
    {
        var view = await Api.GetBacklogAsync($"{Url}?mode=tree");

        view.Review.ShouldBeNull();
    }
}
