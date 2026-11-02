using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;
using static ShipLean.WebApi.Tests.Backlog.Stories.BulkWriteWire;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// The bulk endpoint against the real store: a stale row is left out and reported, a block keeps request order, the whole
// request is one transaction. Each test makes its own stories, so they share a database but not rows.
[TestFixture]
public class BulkWriteApiTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    [Test]
    public async Task Bulk_WhenOneRowStale_ShouldWriteTheOthersAndReportIt()
    {
        var rows = await CreateManyAsync(Api, Project, 3, "Stale");
        await Api.PatchJsonAsync($"api/work-items/{rows[1].Id}", new JsonObject { ["expectedRevision"] = 1, ["changes"] = new JsonObject { ["title"] = new JsonObject { ["value"] = "Taken" } } });

        var body = await BulkAsync(Api, Project, [.. rows.Select(r => Item(r.Id, r.Revision, Points(5), null))]);

        Ids(body.GetProperty("written")).ShouldBe([rows[0].Id, rows[2].Id]);
        Ids(body.GetProperty("stale"), c => c.GetProperty("current")).ShouldBe([rows[1].Id]);
        body.GetProperty("written")[0].GetProperty("revision").GetInt32().ShouldBe(2);
        await using var database = await OpenDatabaseAsync();
        (await RowAsync(database, rows[0].Id)).Points.ShouldBe(5m);
        (await RowAsync(database, rows[2].Id)).Points.ShouldBe(5m);
        (await RowAsync(database, rows[1].Id)).Points.ShouldBeNull();
    }

    [Test]
    public async Task Bulk_WhenBlockMovedToTop_ShouldLandInRequestOrder()
    {
        var rows = await CreateManyAsync(Api, Project, 4, "Top");
        var (a, b, c) = (rows[0].Id, rows[1].Id, rows[2].Id);

        var body = await BulkAsync(Api, Project, [Item(c, 1, null, Slot("first")), Item(a, 1, null, Slot("first"))]);

        body.ShouldReport([c, a], []);
        await using var database = await OpenDatabaseAsync();
        (await BacklogOrderAsync(database, Project)).Take(2).ShouldBe([c, a]);
        (await BacklogOrderAsync(database, Project)).ShouldContain(b);
    }

    [Test]
    public async Task Bulk_WhenBlockMovedBeforeRow_ShouldLandAboveIt()
    {
        var rows = await CreateManyAsync(Api, Project, 4, "Before");
        var (a, b, c, d) = (rows[0].Id, rows[1].Id, rows[2].Id, rows[3].Id);

        var body = await BulkAsync(Api, Project, [Item(d, 1, null, Slot("before", a)), Item(c, 1, null, Slot("before", a))]);

        body.ShouldReport([d, c], []);

        await using var database = await OpenDatabaseAsync();
        var order = await BacklogOrderAsync(database, Project);
        order.IndexOf(c).ShouldBe(order.IndexOf(d) + 1);
        order.IndexOf(a).ShouldBe(order.IndexOf(c) + 1);
        order.ShouldContain(b);
    }

    [Test]
    public async Task Bulk_WhenBlockMovedToBottom_ShouldLandLastInRequestOrder()
    {
        var rows = await CreateManyAsync(Api, Project, 3, "Bottom");

        var body = await BulkAsync(Api, Project, [Item(rows[0].Id, 1, null, Slot("last")), Item(rows[1].Id, 1, null, Slot("last"))]);

        body.ShouldReport([rows[0].Id, rows[1].Id], []);

        await using var database = await OpenDatabaseAsync();
        (await BacklogOrderAsync(database, Project)).TakeLast(2).ShouldBe([rows[0].Id, rows[1].Id]);
    }

    [Test]
    public async Task Bulk_WhenTargetGone_ShouldPlaceLast()
    {
        var rows = await CreateManyAsync(Api, Project, 2, "Gone");

        // New rows land on top, so rows[1] sits above rows[0]; move rows[0] to the top first so it is not already last.
        await BulkAsync(Api, Project, [Item(rows[0].Id, 1, null, Slot("first"))]);
        await using var database = await OpenDatabaseAsync();
        (await BacklogOrderAsync(database, Project))[0].ShouldBe(rows[0].Id);

        var body = await BulkAsync(Api, Project, [Item(rows[0].Id, 1, null, Slot("before", Guid.NewGuid()))]);

        body.ShouldReport([rows[0].Id], []);
        (await BacklogOrderAsync(database, Project))[^1].ShouldBe(rows[0].Id);
    }

    [Test]
    public async Task Bulk_WhenTargetInBlock_ShouldAnswerValidation()
    {
        var rows = await CreateManyAsync(Api, Project, 2, "Block");
        await using var database = await OpenDatabaseAsync();
        var orderBefore = await BacklogOrderAsync(database, Project);

        using var response = await PostAsync(Api, Project, [Item(rows[0].Id, 1, null, Slot("before", rows[0].Id)), Item(rows[1].Id, 1, null, Slot("before", rows[0].Id))]);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await BacklogOrderAsync(database, Project)).ShouldBe(orderBefore);
    }

    [Test]
    public async Task Bulk_WhenSprintChanged_ShouldMoveTasksAsAPatchDoes()
    {
        var single = await CreateAsync(Api, Project, "Patched story");
        var taskOfSingle = await CreateAsync(Api, Project, "Patched task", "task", single.Id);
        var viaBulk = await CreateAsync(Api, Project, "Bulk story");
        var taskOfBulk = await CreateAsync(Api, Project, "Bulk task", "task", viaBulk.Id);
        var other = await CreateAsync(Api, Project, "Bulk partner");
        using var patch = await Api.PatchJsonAsync($"api/work-items/{single.Id}", new JsonObject { ["expectedRevision"] = single.Revision, ["changes"] = Sprint(SeedIds.Sprints.Sprint26) });
        patch.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await BulkAsync(Api, Project, [Item(viaBulk.Id, viaBulk.Revision, Sprint(SeedIds.Sprints.Sprint26), null), Item(other.Id, other.Revision, Sprint(SeedIds.Sprints.Sprint26), null)]);

        body.ShouldReport([viaBulk.Id, other.Id], []);

        await using var database = await OpenDatabaseAsync();
        (await RowAsync(database, viaBulk.Id)).Sprint.ShouldBe(SeedIds.Sprints.Sprint26);
        (await RowAsync(database, other.Id)).Sprint.ShouldBe(SeedIds.Sprints.Sprint26);
        // A single PATCH leaves a story's tasks alone (the tasks follow their story when read); the bulk write must do the same.
        (await RowAsync(database, taskOfBulk.Id)).Sprint.ShouldBe((await RowAsync(database, taskOfSingle.Id)).Sprint);
        (await RowAsync(database, taskOfBulk.Id)).Revision.ShouldBe(1);
    }

    [Test]
    public async Task Bulk_WhenPositionOnly_ShouldWriteNoRevision()
    {
        var rows = await CreateManyAsync(Api, Project, 3, "Position");

        // New rows land on top, so the order starts as rows[2], rows[1], rows[0]; asking for rows[0], rows[1] on top reverses it.
        await using var database = await OpenDatabaseAsync();
        (await BacklogOrderAsync(database, Project)).Take(3).ShouldBe([rows[2].Id, rows[1].Id, rows[0].Id]);

        var body = await BulkAsync(Api, Project, [Item(rows[0].Id, 1, null, Slot("first")), Item(rows[1].Id, 1, null, Slot("first"))]);

        body.ShouldReport([rows[0].Id, rows[1].Id], []);
        body.GetProperty("written")[0].GetProperty("revision").GetInt32().ShouldBe(1);
        body.GetProperty("written")[0].GetProperty("changes").GetArrayLength().ShouldBe(0);
        (await BacklogOrderAsync(database, Project)).Take(3).ShouldBe([rows[0].Id, rows[1].Id, rows[2].Id]);
        foreach (var row in rows)
        {
            (await RowAsync(database, row.Id)).Revision.ShouldBe(1);
            (await RevisionsAsync(database, row.Id)).Select(r => r.Rev).ShouldBe([1]);
        }
    }

    [Test]
    public async Task Bulk_WhenBadParentOnOneRow_ShouldWriteNothing()
    {
        var rows = await CreateManyAsync(Api, Project, 2, "Parent");
        var badParent = new JsonObject { ["parent"] = new JsonObject { ["value"] = Guid.NewGuid() } };

        await using var database = await OpenDatabaseAsync();
        var orderBefore = await BacklogOrderAsync(database, Project);

        using var response = await PostAsync(Api, Project, [Item(rows[0].Id, 1, Points(8), Slot("last")), Item(rows[1].Id, 1, badParent, null)]);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        (await RowAsync(database, rows[0].Id)).ShouldBe((1, null, null));
        (await BacklogOrderAsync(database, Project)).ShouldBe(orderBefore);
    }

    [Test]
    public async Task Bulk_WhenOneItem_ShouldWriteLikeAnEdit()
    {
        var one = await CreateAsync(Api, Project, "Alone");

        var body = await BulkAsync(Api, Project, [Item(one.Id, one.Revision, Points(3), null)]);

        body.ShouldReport([one.Id], []);

        await using var database = await OpenDatabaseAsync();
        (await RevisionsAsync(database, one.Id)).ShouldBe([(1, false), (2, false)]);
    }

    [Test]
    public async Task Bulk_WhenManyItems_ShouldMarkRevisionsBulk()
    {
        var rows = await CreateManyAsync(Api, Project, 2, "Many");

        var body = await BulkAsync(Api, Project, [.. rows.Select(r => Item(r.Id, r.Revision, Points(2), null))]);

        body.ShouldReport([rows[0].Id, rows[1].Id], []);

        await using var database = await OpenDatabaseAsync();
        (await RevisionsAsync(database, rows[0].Id))[^1].ShouldBe((2, true));
        (await RevisionsAsync(database, rows[1].Id))[^1].ShouldBe((2, true));
    }

    [Test]
    public async Task Bulk_WhenWritten_ShouldPublishOneEventPerRowAfterCommit()
    {
        var rows = await CreateManyAsync(Api, Project, 4, "Events");
        var updates = new List<(Guid Id, int EventRevision, int StoredRevision)>();
        var reorders = new List<Guid>();
        var marker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var expire = timeout.Token.Register(() => marker.TrySetCanceled());
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(Project, async projectEvent =>
        {
            if (projectEvent is WorkItemUpdated updated)
            {
                // Read on another connection when the event arrives: a write that had not committed would show its old revision.
                await using var reader = await OpenDatabaseAsync();
                var stored = (await RowAsync(reader, updated.Id)).Revision;
                lock (updates)
                {
                    updates.Add((updated.Id, updated.Revision, stored));
                }

                if (updated.Id == rows[3].Id)
                {
                    marker.TrySetResult();
                }
            }
            else if (projectEvent is WorkItemReordered reordered)
            {
                lock (updates)
                {
                    reorders.Add(reordered.Id);
                }
            }
        });

        var body = await BulkAsync(Api, Project, [Item(rows[0].Id, 1, Points(1), null), Item(rows[1].Id, 1, Points(1), null), Item(rows[2].Id, 1, null, Slot("first"))]);
        body.ShouldReport([rows[0].Id, rows[1].Id, rows[2].Id], []);
        // The bus delivers to one subscriber in publish order. This subscription handles the later write's event after everything the
        // bulk write published, so waiting on its own marker (not on another subscriber) means its lists are complete.
        (await BulkAsync(Api, Project, [Item(rows[3].Id, 1, Points(1), null)])).ShouldReport([rows[3].Id], []);
        await marker.Task;

        lock (updates)
        {
            updates.Where(u => u.Id != rows[3].Id).Select(u => u.Id).ShouldBe([rows[0].Id, rows[1].Id]);
            updates.ShouldAllBe(u => u.EventRevision == 2 && u.StoredRevision == 2);
            reorders.ShouldBe([rows[2].Id]);
        }
    }
}
