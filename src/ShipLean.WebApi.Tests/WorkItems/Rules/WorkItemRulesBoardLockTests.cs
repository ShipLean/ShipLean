using Dapper;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Features.WorkItems.Rules;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Tests.WorkItems.Store;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Rules;

// A board settings put holds the board row FOR UPDATE; a card placement reads the board FOR SHARE and so waits for it.
[TestFixture]
public class WorkItemRulesBoardLockTests : PostgresWorkItemStoreTestBase
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [Test]
    public async Task Patch_WhenBoardPutHoldsTheBoard_ShouldWaitForItAndSeeItsColumns()
    {
        var store = Arrangement.CreateStore(new WorkItemRules(), Actor);
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));

        await using var putter = await Arrangement.Database.OpenAsync();
        await using var put = await putter.BeginTransactionAsync();
        await putter.ExecuteAsync("SELECT 1 FROM board WHERE project_id = @id AND level = 'stories' FOR UPDATE", new { id = project.ProjectId }, put);
        await putter.ExecuteAsync(
            "UPDATE board SET settings = jsonb_set(settings, '{columns}', (settings->'columns') - 2) WHERE project_id = @id AND level = 'stories'",
            new { id = project.ProjectId },
            put);

        var patch = PatchAsync(store, story.Id, 2, new WorkItemChanges { State = new(project.StateId(WorkItemTypeId.Story, StateCategory.Completed)) });
        await WaitUntilBlockedOnBoardOrFinishedAsync(patch);

        patch.IsCompleted.ShouldBeFalse("the patch must wait for the board put");
        await put.CommitAsync();
        var result = await patch;

        var moved = await store.GetAsync(story.Id);
        moved.ColumnId.ShouldBeNull();
        result.Notices.ShouldContain("Left the board: no column maps Closed");
    }

    // Polls the server (each poll is a round trip, no sleep) until a backend waits on a lock in a board read, or the patch ends.
    private async Task WaitUntilBlockedOnBoardOrFinishedAsync(Task patch)
    {
        using var timeout = new CancellationTokenSource(_timeout);
        while (!patch.IsCompleted && !timeout.IsCancellationRequested)
        {
            var waiting = await QueryAsync<int>(
                "SELECT count(*)::int FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query ILIKE '%FROM board%'",
                new { });
            if (waiting > 0)
            {
                return;
            }

            await Task.Yield();
        }
    }
}
