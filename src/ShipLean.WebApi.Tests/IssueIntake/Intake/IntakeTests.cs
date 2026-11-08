using System.Data.Common;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Webhooks;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.IssueIntake;

namespace ShipLean.WebApi.Tests.IssueIntake.Intake;

// DbIssueIntake on the real stores: a new issue becomes a Triage item with its record and a round request in one transaction,
// edits, closes and reopens are applied once, and "/shiplean" takes or restarts an issue. Each test takes its own issue number.
[TestFixture]
public class IntakeTests : ApiFixture
{
    private const string Repository = "shiplean-tests/intake";
    private const string Elsewhere = "shiplean-tests/other";

    private static int _number = 1000;

    private readonly RecordingProposalSessions _rounds = new();
    private readonly FailOnceProposalSessions _failOnce = new();
    private readonly FixedIssueIntakeSettings _settings = new FixedIssueIntakeSettings()
        .Take(new IssueIntakeProject(SeedIds.Projects.Ship, "SHIP", Repository, SeedIds.Users.Joel, "Write a proposal.", 5));

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IIssueIntakeSettings>(_settings);
        services.AddSingleton<IIssueProposalSessions>(_failOnce);
    };

    [SetUp]
    public async Task ArrangeTriageStatesAsync()
    {
        _failOnce.Inner = _rounds;
        _failOnce.FailNext = false;
        _rounds.Next = RoundRequest.Requested;
        await using var connection = await OpenDatabaseAsync();
        foreach (var type in new[] { "story", "bug" })
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO state (id, project_id, type, name, category, sort_order, is_intake)
                SELECT @id, @project, @type, 'Triage', 'proposed', -1, true
                WHERE NOT EXISTS (SELECT 1 FROM state WHERE project_id = @project AND type = @type AND is_intake)
                """,
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, type });
        }
    }

    [Test]
    public async Task Opened_WhenRepositoryTakesIssues_ShouldCreateTriageItemAndStartRound()
    {
        var issue = Event(IssueAction.Opened, body: "Line one\nLine two");

        var outcome = await OpenedAsync(issue);

        outcome.Outcome.ShouldBe(WebhookOutcome.Processed);
        var item = await ItemAsync(outcome.Item!);
        item.Type.ShouldBe("story");
        item.StateName.ShouldBe("Triage");
        item.Title.ShouldBe(issue.Title);
        item.Description.ShouldBe("Reported on GitHub by @reporter\n\n> Line one\n> Line two");
        var record = (await RecordAsync(issue.Issue))!;
        record.ShouldSatisfyAllConditions(
            () => record.WorkItemId.ShouldBe(item.Id),
            () => record.ProjectId.ShouldBe(SeedIds.Projects.Ship),
            () => record.Phase.ShouldBe(IssuePhase.Proposing),
            () => record.AuthorLogin.ShouldBe("reporter"),
            () => record.Title.ShouldBe(issue.Title),
            () => record.Body.ShouldBe("Line one\nLine two"));
        _rounds.Requests.ShouldContain(new RecordedRoundRequest(record.Id, false));
        (await LinkUrlsAsync(item.Id)).ShouldBe([issue.Url]);
    }

    [Test]
    public async Task Opened_WhenBugLabel_ShouldCreateBug()
    {
        var outcome = await OpenedAsync(Event(IssueAction.Opened, labels: ["ui", "Bug"]));

        (await ItemAsync(outcome.Item!)).Type.ShouldBe("bug");
    }

    [Test]
    public async Task Opened_WhenRepositoryNotTaken_ShouldIgnore()
    {
        var issue = Event(IssueAction.Opened, repository: Elsewhere);

        var outcome = await OpenedAsync(issue);

        outcome.ShouldBe(IssueOutcome.Ignored);
        (await RecordAsync(issue.Issue)).ShouldBeNull();
    }

    [Test]
    public async Task Opened_WhenRoundHeld_ShouldWaitForMaintainer()
    {
        _rounds.Next = RoundRequest.Held("rateLimited");
        var issue = Event(IssueAction.Opened);

        await OpenedAsync(issue);

        (await RecordAsync(issue.Issue))!.Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
    }

    [Test]
    public async Task Opened_WhenConcurrentDuplicates_ShouldCreateOnce()
    {
        var issue = Event(IssueAction.Opened);

        var outcomes = await Task.WhenAll(OpenedAsync(issue), OpenedAsync(issue));

        outcomes[0].Item.ShouldNotBeNull();
        outcomes[1].Item.ShouldBe(outcomes[0].Item);
        (await ItemsTitledAsync(issue.Title)).ShouldBe(1);
        var record = (await RecordAsync(issue.Issue))!;
        _rounds.Requests.Count(r => r.IssueId == record.Id).ShouldBe(1);
    }

    [Test]
    public async Task Opened_WhenCrashAfterRecord_ShouldFinishOnRetry()
    {
        var issue = Event(IssueAction.Opened);
        _failOnce.FailNext = true;

        await Should.ThrowAsync<InvalidOperationException>(OpenedAsync(issue));
        (await RecordAsync(issue.Issue)).ShouldBeNull();
        (await ItemsTitledAsync(issue.Title)).ShouldBe(0);
        var outcome = await OpenedAsync(issue);

        outcome.Outcome.ShouldBe(WebhookOutcome.Processed);
        (await ItemsTitledAsync(issue.Title)).ShouldBe(1);
        (await RecordAsync(issue.Issue))!.Phase.ShouldBe(IssuePhase.Proposing);
    }

    [Test]
    public async Task Opened_WhenReplayed_ShouldChangeNothing()
    {
        var issue = Event(IssueAction.Opened);
        var first = await OpenedAsync(issue);

        var second = await OpenedAsync(issue);

        second.ShouldBe(first);
        (await ItemsTitledAsync(issue.Title)).ShouldBe(1);
    }

    [Test]
    public async Task Take_WhenHeld_ShouldRequestForcedRound()
    {
        _rounds.Next = RoundRequest.Held("rateLimited");
        var issue = Event(IssueAction.Opened);
        await OpenedAsync(issue);
        _rounds.Next = RoundRequest.Requested;

        var outcome = await TakeAsync(Comment(issue));

        outcome.Outcome.ShouldBe(WebhookOutcome.Processed);
        var record = (await RecordAsync(issue.Issue))!;
        record.Phase.ShouldBe(IssuePhase.Proposing);
        _rounds.Requests.ShouldContain(new RecordedRoundRequest(record.Id, true));
    }

    [Test]
    public async Task Take_WhenNoRecord_ShouldCreateItemAndForceRound()
    {
        var comment = Comment(Event(IssueAction.Opened));

        var outcome = await TakeAsync(comment);

        outcome.Outcome.ShouldBe(WebhookOutcome.Processed);
        var record = (await RecordAsync(comment.Issue))!;
        _rounds.Requests.ShouldContain(new RecordedRoundRequest(record.Id, true));
    }

    [Test]
    public async Task Take_WhenAccepted_ShouldIgnore()
    {
        var issue = Event(IssueAction.Opened);
        await OpenedAsync(issue);
        var record = (await RecordAsync(issue.Issue))!;
        await SetPhaseAsync(record.Id, IssuePhase.Accepted);
        var before = _rounds.Requests.Count;

        var outcome = await TakeAsync(Comment(issue));

        outcome.ShouldBe(IssueOutcome.Ignored);
        _rounds.Requests.Count.ShouldBe(before);
        (await RecordAsync(issue.Issue))!.Phase.ShouldBe(IssuePhase.Accepted);
    }

    [Test]
    public async Task Take_WhenRepositoryNotTaken_ShouldIgnore()
    {
        var comment = Comment(Event(IssueAction.Opened, repository: Elsewhere));

        (await TakeAsync(comment)).ShouldBe(IssueOutcome.Ignored);
    }

    [Test]
    public async Task Closed_ShouldSetNotOpen()
    {
        var issue = Event(IssueAction.Opened);
        var item = (await OpenedAsync(issue)).Item!;

        var outcome = await ChangedAsync(issue with { Action = IssueAction.Closed, Open = false });

        outcome.Item.ShouldBe(item);
        (await RecordAsync(issue.Issue))!.OpenOnGitHub.ShouldBeFalse();
        (await CommentsAsync(item)).ShouldBe(["The issue was closed on GitHub."]);
    }

    [Test]
    public async Task Reopened_WhenClosed_ShouldSetOpen()
    {
        var issue = Event(IssueAction.Opened);
        var item = (await OpenedAsync(issue)).Item!;
        await ChangedAsync(issue with { Action = IssueAction.Closed, Open = false });

        await ChangedAsync(issue with { Action = IssueAction.Reopened, UpdatedAt = issue.UpdatedAt.AddMinutes(1) });

        (await RecordAsync(issue.Issue))!.OpenOnGitHub.ShouldBeTrue();
        (await CommentsAsync(item))[^1].ShouldBe("The issue was reopened on GitHub.");
    }

    [Test]
    public async Task Edited_WhenReplayed_ShouldCommentOnce()
    {
        var issue = Event(IssueAction.Opened);
        var item = (await OpenedAsync(issue)).Item!;
        var edited = issue with { Action = IssueAction.Edited, Title = "New title", Body = "New body", UpdatedAt = issue.UpdatedAt.AddMinutes(2) };

        await ChangedAsync(edited);
        await ChangedAsync(edited);

        var record = (await RecordAsync(issue.Issue))!;
        record.Title.ShouldBe("New title");
        record.Body.ShouldBe("New body");
        (await CommentsAsync(item)).ShouldBe(["The issue was edited on GitHub. New title: New title\n\n> New body"]);
    }

    [Test]
    public async Task Edited_WhenStaleAfterNewer_ShouldKeepNewerText()
    {
        var issue = Event(IssueAction.Opened);
        var item = (await OpenedAsync(issue)).Item!;
        var newer = issue with { Action = IssueAction.Edited, Title = "Newer", Body = "Newer body", UpdatedAt = issue.UpdatedAt.AddMinutes(5) };
        var older = issue with { Action = IssueAction.Edited, Title = "Older", Body = "Older body", UpdatedAt = issue.UpdatedAt.AddMinutes(2) };

        await ChangedAsync(newer);
        await ChangedAsync(older);

        (await RecordAsync(issue.Issue))!.Title.ShouldBe("Newer");
        (await CommentsAsync(item)).ShouldBe(["The issue was edited on GitHub. New title: Newer\n\n> Newer body"]);
    }

    // The order is strictly by GitHub's updated_at, which has one-second resolution: on a tie the first delivery wins.
    [Test]
    public async Task Edited_WhenSameTimestampDifferentText_ShouldApplyOnce()
    {
        var issue = Event(IssueAction.Opened);
        var item = (await OpenedAsync(issue)).Item!;
        var at = issue.UpdatedAt.AddMinutes(2);

        await ChangedAsync(issue with { Action = IssueAction.Edited, Title = "First", Body = "One", UpdatedAt = at });
        await ChangedAsync(issue with { Action = IssueAction.Edited, Title = "Second", Body = "Two", UpdatedAt = at });

        (await RecordAsync(issue.Issue))!.Title.ShouldBe("First");
        (await CommentsAsync(item)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task ClosedThenReopened_WhenDeliveredReversed_ShouldEndOpen()
    {
        // The reopen (+3 minutes) arrives first; the older close (+1 minute) then arrives and must lose.
        var other = Event(IssueAction.Opened);
        var otherItem = (await OpenedAsync(other)).Item!;
        await ChangedAsync(other with { Action = IssueAction.Closed, Open = false, UpdatedAt = other.UpdatedAt });
        await ChangedAsync(other with { Action = IssueAction.Reopened, UpdatedAt = other.UpdatedAt.AddMinutes(3) });
        await ChangedAsync(other with { Action = IssueAction.Closed, Open = false, UpdatedAt = other.UpdatedAt.AddMinutes(1) });

        (await RecordAsync(other.Issue))!.OpenOnGitHub.ShouldBeTrue();
        (await CommentsAsync(otherItem)).ShouldBe(["The issue was closed on GitHub.", "The issue was reopened on GitHub."]);
    }

    [Test]
    public async Task ClosedAndReopened_WhenConcurrent_ShouldFollowNewestTimestamp()
    {
        var issue = Event(IssueAction.Opened);
        var item = (await OpenedAsync(issue)).Item!;
        var record = (await RecordAsync(issue.Issue))!;
        await using var holder = await OpenDatabaseAsync();
        await using var transaction = await holder.BeginTransactionAsync();
        await using var scope = Host.Services.CreateAsyncScope();

        // The newer reopen has written its row but not committed; the older close then runs on its own connection and must wait for it.
        (await scope.ServiceProvider.GetRequiredService<IGitHubIssueStore>()
            .SetOpenOnGitHubAsync(record.Id, true, issue.UpdatedAt.AddMinutes(2), transaction)).ShouldBeTrue();
        var close = ChangedAsync(issue with { Action = IssueAction.Closed, Open = false, UpdatedAt = issue.UpdatedAt.AddMinutes(1) });
        await using var observer = await OpenDatabaseAsync();
        while (!close.IsCompleted && await observer.ExecuteScalarAsync<int>("SELECT count(*)::int FROM pg_locks WHERE NOT granted") == 0)
        {
            await Task.Yield();
        }

        await transaction.CommitAsync();
        await close;

        (await RecordAsync(issue.Issue))!.OpenOnGitHub.ShouldBeTrue();
        (await CommentsAsync(item)).ShouldBeEmpty();
    }

    [TestCase(IssueAction.Deleted)]
    [TestCase(IssueAction.Transferred)]
    public async Task Gone_WhenDeletedOrTransferred_ShouldSetNotOpenAndKeepItem(IssueAction action)
    {
        var issue = Event(IssueAction.Opened);
        var item = (await OpenedAsync(issue)).Item!;

        await ChangedAsync(issue with { Action = action });

        (await RecordAsync(issue.Issue))!.OpenOnGitHub.ShouldBeFalse();
        (await ItemAsync(item)).StateName.ShouldBe("Triage");
        (await CommentsAsync(item)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Changed_WhenNoRecord_ShouldIgnore()
    {
        (await ChangedAsync(Event(IssueAction.Edited))).ShouldBe(IssueOutcome.Ignored);
    }

    private static IssueEvent Event(IssueAction action, string repository = Repository, string body = "A body", IReadOnlyList<string>? labels = null)
    {
        var number = Interlocked.Increment(ref _number);
        return new IssueEvent(
            action, new IssueRef(IssueRef.GitHubHost, repository, number), $"https://github.com/{repository}/issues/{number}", $"Issue title {repository} {number}", body,
            "reporter", "NONE", labels ?? [], true, null, "reporter", false, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
    }

    private static IssueCommentEvent Comment(IssueEvent issue)
        => new(issue.Issue, issue.Url, issue.Title, issue.AuthorLogin, true, false, 1, issue.Url + "#c1", "/shiplean", "maintainer", "OWNER", false, issue.UpdatedAt);

    private async Task<IssueOutcome> OpenedAsync(IssueEvent issue)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIssueIntake>().OpenedAsync(issue, CancellationToken.None);
    }

    private async Task<IssueOutcome> ChangedAsync(IssueEvent issue)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIssueIntake>().ChangedAsync(issue, CancellationToken.None);
    }

    private async Task<IssueOutcome> TakeAsync(IssueCommentEvent comment)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIssueIntake>().TakeAsync(comment, CancellationToken.None);
    }

    private async Task<GitHubIssueRecord?> RecordAsync(IssueRef issue)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IGitHubIssueStore>().FindAsync(issue);
    }

    private async Task<ItemRow> ItemAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<ItemRow>(
            """
            SELECT w.id, w.type, w.title, w.description, s.name AS stateName
            FROM work_item w JOIN state s ON s.id = w.state_id
            WHERE w.project_id = @project AND @key = 'SHIP-' || w.number
            """,
            new { project = SeedIds.Projects.Ship, key });
    }

    private async Task<int> ItemsTitledAsync(string title)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE title = @title", new { title });
    }

    private async Task<IReadOnlyList<string>> LinkUrlsAsync(Guid itemId)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<string>("SELECT url FROM work_item_link WHERE source_id = @itemId AND url IS NOT NULL", new { itemId })];
    }

    private async Task<IReadOnlyList<string>> CommentsAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<string>(
            "SELECT c.body FROM comment c JOIN work_item w ON w.id = c.work_item_id WHERE w.project_id = @project AND @key = 'SHIP-' || w.number ORDER BY c.created_at, c.id",
            new { project = SeedIds.Projects.Ship, key })];
    }

    private async Task SetPhaseAsync(Guid id, IssuePhase phase)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IGitHubIssueStore>().SetPhaseAsync(id, [IssuePhase.Proposing], phase);
    }

    private sealed record ItemRow(Guid Id, string Type, string Title, string Description, string StateName);

    // Delegates to the recording double, and throws once on request, as a crash after the record would.
    private sealed class FailOnceProposalSessions : IIssueProposalSessions
    {
        public IIssueProposalSessions Inner { get; set; } = null!;

        public bool FailNext { get; set; }

        public Task<RoundRequest> RequestRoundAsync(Guid issueId, bool force, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("crash after the record");
            }

            return Inner.RequestRoundAsync(issueId, force, transaction, cancellationToken);
        }
    }
}
