using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.TaskChanges.Migration;
using ShipLean.Application.Plans;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TaskChanges;
using static ShipLean.TestSupport.TaskChanges.FakeCodeHost;

namespace ShipLean.WebApi.Tests.TaskChanges.Migration;

// The one-off job that moves commit link cards into work_item_commit, called directly (it also runs at startup, where it finds nothing).
[TestFixture]
public class CommitCardMigrationTests : ApiFixture
{
    private readonly FakeCodeHost _host = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddFakeCodeHost(_host);

    // The job sweeps every live commit card, so each test starts with none left over from another.
    [SetUp]
    public async Task ArchiveLeftoverCards()
    {
        _host.Outage = null;
        Host.Services.GetRequiredService<CommitCardMigration>().Pause = TimeSpan.Zero;
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE work_item_link SET archived_at = now() WHERE link_type = 'url' AND archived_at IS NULL");
    }

    [Test]
    public async Task CommitCardMigration_WhenReadable_ShouldMoveToCommitsAndArchiveCard()
    {
        var item = await NewItemAsync("task");
        var card = await AddCardAsync(item, StorefrontRepo.CommitUrl(Shas.First));
        var events = new List<ProjectEvent>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(null, e => Record(events, e));

        var (moved, kept) = await RunAsync();

        moved.ShouldBe(1);
        kept.ShouldBe(0);
        var commits = await ListAsync(item);
        commits.ShouldHaveSingleItem().Sha.ShouldBe(Shas.First);
        commits[0].AddedVia.ShouldBe(CommitSource.Migration);
        (await ArchivedAsync(card)).ShouldBeTrue();
        events.OfType<LinksChanged>().Count(e => e.WorkItemId == item).ShouldBe(1);
    }

    [Test]
    public async Task CommitCardMigration_WhenUnreadable_ShouldKeepCard()
    {
        var item = await NewItemAsync("task");
        var card = await AddCardAsync(item, SecretRepo.CommitUrl(Shas.First));
        var missing = await AddCardAsync(item, StorefrontRepo.CommitUrl(Shas.Missing));

        var (moved, kept) = await RunAsync();

        moved.ShouldBe(0);
        kept.ShouldBe(2);
        (await ListAsync(item)).ShouldBeEmpty();
        (await ArchivedAsync(card)).ShouldBeFalse();
        (await ArchivedAsync(missing)).ShouldBeFalse();
    }

    [Test]
    public async Task CommitCardMigration_WhenHostGaveFinalAnswer_ShouldNotReadTheCardAgain()
    {
        var item = await NewItemAsync("task");
        await AddCardAsync(item, SecretRepo.CommitUrl(Shas.First));
        await AddCardAsync(item, StorefrontRepo.CommitUrl(Shas.Missing));

        var before = _host.CommitReads;
        var first = await RunAsync();
        var readsAfterFirst = _host.CommitReads;
        var second = await RunAsync();

        first.ShouldBe((0, 2));
        second.ShouldBe((0, 0));
        (readsAfterFirst - before).ShouldBe(2);
        _host.CommitReads.ShouldBe(readsAfterFirst);
    }

    [Test]
    public async Task CommitCardMigration_WhenRateLimited_ShouldStopAndTryAgainNextRun()
    {
        var item = await NewItemAsync("task");
        var first = await AddCardAsync(item, StorefrontRepo.CommitUrl(Shas.First));
        var second = await AddCardAsync(item, StorefrontRepo.CommitUrl(Shas.Second));
        _host.Outage = 429;
        var before = _host.CommitReads;

        var limited = await RunAsync();

        limited.ShouldBe((0, 0));
        (_host.CommitReads - before).ShouldBe(1);
        (await CheckedAsync(first)).ShouldBeFalse();
        (await CheckedAsync(second)).ShouldBeFalse();

        _host.Outage = null;
        var later = await RunAsync();

        later.ShouldBe((2, 0));
    }

    [Test]
    public async Task CommitCardMigration_WhenNetworkFails_ShouldKeepCardAndReadItAgain()
    {
        var item = await NewItemAsync("task");
        var card = await AddCardAsync(item, StorefrontRepo.CommitUrl(Shas.First));
        _host.Outage = 0;

        var failed = await RunAsync();

        failed.ShouldBe((0, 1));
        (await CheckedAsync(card)).ShouldBeFalse();

        _host.Outage = null;
        (await RunAsync()).ShouldBe((1, 0));
    }

    [Test]
    public async Task CommitCardMigration_WhenRunTwice_ShouldNotDuplicate()
    {
        var item = await NewItemAsync("bug");
        await AddCardAsync(item, StorefrontRepo.CommitUrl(Shas.First));
        await AddCardAsync(item, StorefrontRepo.CommitUrl(Shas.Second));

        var first = await RunAsync();
        var second = await RunAsync();

        first.ShouldBe((2, 0));
        second.ShouldBe((0, 0));
        (await ListAsync(item)).Select(c => c.Sha).ShouldBe([Shas.First, Shas.Second], ignoreOrder: true);
    }

    [Test]
    public async Task CommitCardMigration_WhenLockHeldElsewhere_ShouldSkip()
    {
        var item = await NewItemAsync("task");
        var card = await AddCardAsync(item, StorefrontRepo.CommitUrl(Shas.First));
        await using var holder = await OpenDatabaseAsync();
        (await holder.ExecuteScalarAsync<bool>(CommitCardMigrationLock.TrySql, CommitCardMigrationLock.Parameters)).ShouldBeTrue();

        try
        {
            var result = await RunAsync();

            result.ShouldBe((0, 0));
            (await ListAsync(item)).ShouldBeEmpty();
            (await ArchivedAsync(card)).ShouldBeFalse();
        }
        finally
        {
            await holder.ExecuteScalarAsync<bool>(CommitCardMigrationLock.UnlockSql, CommitCardMigrationLock.Parameters);
        }
    }

    [Test]
    public async Task CommitCardMigration_WhenNotACommitCard_ShouldLeaveIt()
    {
        var item = await NewItemAsync("task");
        var card = await AddCardAsync(item, "https://github.com/acme/storefront/pull/3", PlanMarkers.PullRequest("A pull request"));

        var result = await RunAsync();

        result.ShouldBe((0, 0));
        (await ArchivedAsync(card)).ShouldBeFalse();
    }

    private static Task Record(List<ProjectEvent> events, ProjectEvent projectEvent)
    {
        lock (events)
        {
            events.Add(projectEvent);
        }
        return Task.CompletedTask;
    }

    private async Task<(int Moved, int Kept)> RunAsync() => await Host.Services.GetRequiredService<CommitCardMigration>().RunAsync(CancellationToken.None);

    private async Task<IReadOnlyList<TaskCommit>> ListAsync(Guid item)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITaskCommits>().ListAsync(item);
    }

    private async Task<Guid> NewItemAsync(string type)
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).InsertAsync(connection);
    }

    private async Task<Guid> AddCardAsync(Guid item, string url, string? title = null)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, link_type, url, title) VALUES (@id, @item, 'url', @url, @title)",
            new { id, item, url, title = title ?? PlanMarkers.Commit(Shas.First, "An old card") });
        return id;
    }

    private async Task<bool> CheckedAsync(Guid card)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<bool>("SELECT commit_migration_checked_at IS NOT NULL FROM work_item_link WHERE id = @card", new { card });
    }

    private async Task<bool> ArchivedAsync(Guid card)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<bool>("SELECT archived_at IS NOT NULL FROM work_item_link WHERE id = @card", new { card });
    }
}
