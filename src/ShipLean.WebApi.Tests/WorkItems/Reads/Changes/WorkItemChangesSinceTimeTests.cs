using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems.Reads.Changes;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Reads.Changes;

[TestFixture]
public class WorkItemChangesSinceTimeTests : WorkItemChangesTestBase
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task SinceTime_ShouldKeepEntriesAtOrAfterTheTimeAndGiveAResumableCursor()
    {
        var project = await Arrangement.ArrangeProjectAsync();
        var item = await AddItemAsync(project);
        await AddRevisionAsync(item, 2, T0.AddMinutes(-1), SeedIds.Users.Priya);
        await AddRevisionAsync(item, 3, T0, SeedIds.Users.Priya);
        await AddCommentAsync(item, "On the dot", T0, SeedIds.Users.Priya);
        await AddCommentAsync(item, "Later", T0.AddMinutes(5), SeedIds.Users.Priya);

        var first = await Changes.SinceTimeAsync(project.ProjectId, T0, 2, null);

        first.Entries.Select(e => e.Revision ?? 0).ShouldBe([3, 0]);
        first.Entries[1].Comment.ShouldBe("On the dot");
        first.More.ShouldBeTrue();
        var rest = await Changes.SinceAsync(project.ProjectId, first.Cursor, 100, null);
        rest.Entries.Where(e => e.Comment is not null).Select(e => e.Comment).ShouldBe(["Later"]);
    }

    [Test]
    public async Task SinceTime_WhenNothingFollows_ShouldReturnACursorThatResumesFromThatTime()
    {
        var project = await Arrangement.ArrangeProjectAsync();
        var item = await AddItemAsync(project);
        await AddRevisionAsync(item, 2, T0, SeedIds.Users.Priya);
        var future = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var empty = await Changes.SinceTimeAsync(project.ProjectId, future, 100, null);
        await AddCommentAsync(item, "Much later", future.AddMinutes(1), SeedIds.Users.Priya);
        var resumed = await Changes.SinceAsync(project.ProjectId, empty.Cursor, 100, null);

        empty.Entries.ShouldBeEmpty();
        empty.Cursor.ShouldNotBeNull();
        resumed.Entries.Select(e => e.Comment).ShouldBe(["Much later"]);
    }

    [Test]
    public async Task SinceTime_WhenLimitOutOfRange_ShouldBeValidation()
    {
        var project = await Arrangement.ArrangeProjectAsync();

        var error = await Should.ThrowAsync<ApiException>(() => Changes.SinceTimeAsync(project.ProjectId, T0, 0, null));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("limit");
    }
}
