using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Records;
using ShipLean.Application.Features.WorkItemDetails.Comments;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Sprints;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.IssueIntake.Records;

// DbMirroredComments on the real comment store: one GitHub comment is mirrored once even when two deliveries race, the comment
// and its provenance row commit or roll back together, and commentsChanged waits for the caller's commit.
[TestFixture]
public class MirroredCommentsTests : ApiFixture
{
    [Test]
    public async Task MirroredAdd_WhenConcurrentForOneUrl_ShouldKeepOneComment()
    {
        var comment = await NewCommentAsync();
        await using var firstScope = Host.Services.CreateAsyncScope();
        await using var secondScope = Host.Services.CreateAsyncScope();
        await using var first = await OpenDatabaseAsync();
        await using var second = await OpenDatabaseAsync();
        var secondPid = await second.ExecuteScalarAsync<int>("SELECT pg_backend_pid()");
        await using var firstTransaction = await first.BeginTransactionAsync();
        await using var secondTransaction = await second.BeginTransactionAsync();

        var won = await firstScope.ServiceProvider.GetRequiredService<IMirroredComments>().AddAsync(comment, firstTransaction);
        var losing = secondScope.ServiceProvider.GetRequiredService<IMirroredComments>().AddAsync(comment, secondTransaction);
        await WaitUntilBlockedAsync(secondPid, losing);
        await firstTransaction.CommitAsync();
        var lost = await losing;
        await secondTransaction.CommitAsync();

        lost.CommentId.ShouldBe(won.CommentId);
        lost.Changed.ShouldBeNull();
        won.Changed.ShouldNotBeNull();
        (await CountsAsync(comment)).ShouldBe((1, 1));
    }

    [Test]
    public async Task MirroredAdd_WhenRolledBack_ShouldKeepNeither()
    {
        var comment = await NewCommentAsync();
        await using var scope = Host.Services.CreateAsyncScope();
        await using var database = await OpenDatabaseAsync();
        await using var transaction = await database.BeginTransactionAsync();

        var added = await scope.ServiceProvider.GetRequiredService<IMirroredComments>().AddAsync(comment, transaction);
        await transaction.RollbackAsync();

        added.CommentId.ShouldNotBe(Guid.Empty);
        (await CountsAsync(comment)).ShouldBe((0, 0));
    }

    [Test]
    public async Task CommentsChanged_WhenMirroredAddCommits_ShouldBeLeftToTheCallerAfterCommit()
    {
        var comment = await NewCommentAsync();
        var events = new RecordedProjectEvents();
        await using var scope = Host.Services.CreateAsyncScope();
        await using var database = await OpenDatabaseAsync();
        await using var transaction = await database.BeginTransactionAsync();

        var added = await Mirrored(scope, events).AddAsync(comment, transaction);
        events.Published.ShouldBeEmpty();
        await transaction.CommitAsync();

        events.Published.ShouldBeEmpty();
        added.Changed.ShouldBe(new CommentsChanged(SeedIds.Projects.Ship, SeedIds.WorkItems.Ship89));
        (await CountsAsync(comment)).ShouldBe((1, 1));
    }

    [Test]
    public async Task CommentsChanged_WhenMirroredAddRollsBack_ShouldNeverBePublished()
    {
        var comment = await NewCommentAsync();
        var events = new RecordedProjectEvents();
        await using var scope = Host.Services.CreateAsyncScope();
        await using var database = await OpenDatabaseAsync();
        await using var transaction = await database.BeginTransactionAsync();

        await Mirrored(scope, events).AddAsync(comment, transaction);
        await transaction.RollbackAsync();

        events.Published.ShouldBeEmpty();
        (await CountsAsync(comment)).ShouldBe((0, 0));
    }

    // The real store and mirror over an event bus that records, so a publish inside the transaction would show.
    private static DbMirroredComments Mirrored(AsyncServiceScope scope, RecordedProjectEvents events)
    {
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        var store = new CommentStore(
            dataSource, new FixedCurrentUser(SeedIds.Users.Joel), AgentIdentity.None,
            scope.ServiceProvider.GetRequiredService<MarkdownRenderer>(), TimeProvider.System, events);
        return new DbMirroredComments(dataSource, store);
    }

    private async Task<MirroredComment> NewCommentAsync()
    {
        var issue = await Host.Services.GetRequiredService<IGitHubIssueStore>().InsertAsync(
            new IssueRef(IssueRef.GitHubHost, "shiplean-tests/mirrored", Random.Shared.Next(1, int.MaxValue)), SeedIds.Projects.Ship, SeedIds.WorkItems.Ship89,
            "reporter", "The title", "The body", IssuePhase.Proposing);
        var githubId = Random.Shared.NextInt64(1, long.MaxValue);
        return new MirroredComment(
            issue.Id, githubId, $"https://github.com/{issue.Issue.Repository}/issues/{issue.Issue.Number}#issuecomment-{githubId}", "reporter",
            $"GitHub comment {githubId}", Host.Services.GetRequiredService<TimeProvider>().GetUtcNow(), SeedIds.Users.Joel);
    }

    // Polls pg_locks until the other connection waits on a lock; the losing add must not finish first.
    private async Task WaitUntilBlockedAsync(int pid, Task losing)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var probe = await OpenDatabaseAsync();
        while (!await probe.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = @pid AND NOT granted)", new { pid }, cancellationToken: timeout.Token)))
        {
            losing.IsCompleted.ShouldBeFalse("the second add finished without waiting for the first");
        }
    }

    // (comments with the body, provenance rows with the url)
    private async Task<(int Comments, int Rows)> CountsAsync(MirroredComment comment)
    {
        await using var database = await OpenDatabaseAsync();
        var comments = await database.ExecuteScalarAsync<int>("SELECT count(*)::int FROM comment WHERE body = @Body", new { comment.Body });
        var rows = await database.ExecuteScalarAsync<int>("SELECT count(*)::int FROM github_issue_comment WHERE html_url = @HtmlUrl", new { comment.HtmlUrl });
        return (comments, rows);
    }
}
