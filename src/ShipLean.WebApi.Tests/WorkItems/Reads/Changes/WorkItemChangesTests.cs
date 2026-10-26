using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Changes;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Reads.Changes;

[TestFixture]
public class WorkItemChangesTests : WorkItemChangesTestBase
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ChangesSince_WhenReorderOnly_ShouldBeEmpty()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        var cursor = (await Changes.SinceAsync(project.ProjectId, null, 100, null)).Cursor;

        WorkItemContractSupport.Written(await store.ReorderAsync(second.Id, 1, first.Id));

        var page = await Changes.SinceAsync(project.ProjectId, cursor, 100, null);
        page.Entries.ShouldBeEmpty();
        page.More.ShouldBeFalse();
        page.Cursor.ShouldBe(cursor);
    }

    [Test]
    public async Task ChangesSince_WhenCommentAdded_ShouldIncludeIt()
    {
        var project = await Arrangement.ArrangeProjectAsync();
        var item = await AddItemAsync(project, b => b.WithTitle("Pay by card"));
        await AddRevisionAsync(item, 2, T0, SeedIds.Users.Priya);
        await AddCommentAsync(item, "Looks **good**", T0.AddMinutes(5), SeedIds.Users.Priya);
        await AddCommentAsync(item, "Withdrawn", T0.AddMinutes(6), SeedIds.Users.Priya);
        await ArchiveCommentsAsync(item, "Withdrawn");

        var page = await Changes.SinceAsync(project.ProjectId, null, 100, null);

        page.Entries.Select(e => e.Kind).ShouldBe([ChangeKind.Revision, ChangeKind.Comment, ChangeKind.Revision]);  // rev 2 and the comment at T0, then the arranged item's creation (now)
        var revision = page.Entries.Single(e => e.At == T0);
        revision.Fields.ShouldNotBeNull().Single().Field.ShouldBe("points");
        revision.Comment.ShouldBeNull();
        revision.ActorName.ShouldBe("Priya Shah");
        var comment = page.Entries.Single(e => e.Kind == ChangeKind.Comment);
        comment.Comment.ShouldBe("Looks **good**");
        comment.Fields.ShouldBeNull();
        comment.Title.ShouldBe("Pay by card");
        comment.Key.ShouldStartWith(project.Key + "-");
        comment.ActorId.ShouldBe(SeedIds.Users.Priya);
        comment.At.ShouldBe(T0.AddMinutes(5));
    }

    [Test]
    public async Task ChangesSince_ShouldReturnCursorThatResumesWithoutGapsOrRepeats()
    {
        var project = await Arrangement.ArrangeProjectAsync();
        var items = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var item = await AddItemAsync(project);
            items.Add(item);
            await AddRevisionAsync(item, 2, T0);
            await AddRevisionAsync(item, 3, T0);
            await AddCommentAsync(item, $"Comment {i}", T0);
        }

        var all = await Changes.SinceAsync(project.ProjectId, null, 500, null);
        var seen = new List<ChangeEntryDto>();
        string? cursor = null;
        var pages = 0;
        bool more;
        do
        {
            var page = await Changes.SinceAsync(project.ProjectId, cursor, 4, null);
            page.Entries.Count.ShouldBeLessThanOrEqualTo(4);
            seen.AddRange(page.Entries);
            cursor = page.Cursor;
            more = page.More;
            pages++;
        }
        while (more);

        all.Entries.Count.ShouldBe(12);
        pages.ShouldBe(3);
        seen.Count.ShouldBe(12);
        seen.Select(e => (e.WorkItemId, e.Kind, e.At, e.Revision, e.Comment)).ShouldBe(all.Entries.Select(e => (e.WorkItemId, e.Kind, e.At, e.Revision, e.Comment)));
        (await Changes.SinceAsync(project.ProjectId, cursor, 4, null)).Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task ChangesSince_WhenMine_ShouldOnlyIncludeMyItems()
    {
        var project = await Arrangement.ArrangeProjectAsync();
        var assigned = await AddItemAsync(project, b => b.WithAssignee(SeedIds.Users.Priya).WithTitle("Assigned"));
        var other = await AddItemAsync(project, b => b.WithAssignee(SeedIds.Users.Joel).WithTitle("Not mine"));
        await AddCommentAsync(assigned, "Hello", T0);
        await AddCommentAsync(other, "Elsewhere", T0);
        await ExecuteAsync("UPDATE work_item SET created_by = @priya WHERE id = @id", new { priya = SeedIds.Users.Priya, id = other });

        var third = await AddItemAsync(project, b => b.WithAssignee(SeedIds.Users.Joel).WithTitle("Joel's"));

        var mine = await Changes.SinceAsync(project.ProjectId, null, 100, SeedIds.Users.Priya);

        mine.Entries.Select(e => e.WorkItemId).Distinct().OrderBy(id => id).ShouldBe(new[] { assigned, other }.OrderBy(id => id));
        mine.Entries.ShouldNotContain(e => e.WorkItemId == third);
        (await Changes.SinceAsync(project.ProjectId, null, 100, null)).Entries.ShouldContain(e => e.WorkItemId == third);
    }

    [Test]
    public async Task ChangesSince_WhenForeignProject_ShouldReturnNothing()
    {
        var project = await Arrangement.ArrangeProjectAsync();
        var other = await Arrangement.ArrangeProjectAsync();
        var item = await AddItemAsync(project);
        await AddCommentAsync(item, "Private", T0);

        (await Changes.SinceAsync(other.ProjectId, null, 100, null)).Entries.ShouldBeEmpty();
        (await Changes.SinceAsync(project.ProjectId, null, 100, null)).Entries.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ChangesSince_WhenCursorIsGarbageOrLimitOutOfRange_ShouldBeValidation()
    {
        var project = await Arrangement.ArrangeProjectAsync();

        (await Should.ThrowAsync<ApiException>(() => Changes.SinceAsync(project.ProjectId, "not a cursor!", 10, null))).Code.ShouldBe(ApiErrorCodes.Validation);
        (await Should.ThrowAsync<ApiException>(() => Changes.SinceAsync(project.ProjectId, "bm9wZQ", 10, null))).Code.ShouldBe(ApiErrorCodes.Validation);
        (await Should.ThrowAsync<ApiException>(() => Changes.SinceAsync(project.ProjectId, null, 0, null))).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    private Task ArchiveCommentsAsync(Guid itemId, string body)
        => ExecuteAsync("UPDATE comment SET archived_at = now() WHERE work_item_id = @itemId AND body = @body", new { itemId, body });
}
