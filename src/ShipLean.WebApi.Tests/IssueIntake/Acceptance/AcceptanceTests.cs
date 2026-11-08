using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Acceptance;
using ShipLean.Application.Features.IssueIntake.Records;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.IssueIntake;

namespace ShipLean.WebApi.Tests.IssueIntake.Acceptance;

// The acceptance lane on the real stores (D6, D18, D19): an LGTM enqueues one accept job, and AcceptJobWorker applies the latest
// proposal step by step, resumable after a crash at any step, with the phase set last. Each test opens its own issue through
// intake, so it starts on a real Triage item; the worker claims any due accept job, so each test ends with none pending.
[TestFixture]
public class AcceptanceTests : ApiFixture
{
    private static int _number;

    private readonly string _repository = $"shiplean-tests/accept-{Guid.NewGuid():N}";
    private readonly RecordingProposalSessions _rounds = new();
    private FixedIssueIntakeSettings _settings = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        _settings = new FixedIssueIntakeSettings().Take(new IssueIntakeProject(SeedIds.Projects.Ship, "SHIP", _repository, SeedIds.Users.Joel, "Write a proposal.", 5));
        services.AddSingleton<IIssueIntakeSettings>(_settings);
        services.AddSingleton<IIssueProposalSessions>(_rounds);
        services.AddSingleton(sp => new CrashingIssueStore(ActivatorUtilities.CreateInstance<DbGitHubIssueStore>(sp)));
        services.AddSingleton<IGitHubIssueStore>(sp => sp.GetRequiredService<CrashingIssueStore>());
    };

    private CrashingIssueStore Store => Host.Services.GetRequiredService<CrashingIssueStore>();

    private AcceptJobWorker Worker => Host.Services.GetRequiredService<AcceptJobWorker>();

    [SetUp]
    public async Task ArrangeTriageStatesAsync()
    {
        Store.CrashOnRepoint = false;
        Store.CrashOnAccepted = false;
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

    [TearDown]
    public async Task CompleteLeftoversAsync()
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE status = 'pending' AND kind = 'accept'");
    }

    [Test]
    public async Task Accept_WhenSameType_ShouldMoveToBacklogWithCriteria()
    {
        var parent = await CreateItemAsync(SeedIds.Projects.Ship, WorkItemTypeId.Feature, "Accept parent feature");
        var issue = await ArrangeAsync(Proposal("new", "story", "Sign in says Sign in", parentKey: parent.Key, criteria: ["The button says Sign in", "It is translated"]));

        (await AcceptAsync(issue)).Outcome.ShouldBe(WebhookOutcome.Processed);
        (await Worker.RunNextAsync(CancellationToken.None)).ShouldBeTrue();

        var item = await ItemAsync(issue.WorkItemId);
        var acceptedState = await AcceptedStateNameAsync("story");
        item.ShouldSatisfyAllConditions(
            () => item.Title.ShouldBe("Sign in says Sign in"),
            () => item.Description.ShouldBe("The description."),
            () => item.StateName.ShouldBe(acceptedState),
            () => item.IsIntake.ShouldBeFalse(),
            () => item.ParentId.ShouldBe(parent.Id),
            () => item.Criteria.ShouldContain("The button says Sign in"),
            () => item.Criteria.ShouldContain("It is translated"));
        var record = await RecordAsync(issue.Id);
        record.Phase.ShouldBe(IssuePhase.Accepted);
        record.WorkItemId.ShouldBe(issue.WorkItemId);
        var reply = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        reply.DedupeKey.ShouldBe(AcceptanceTexts.ReplyKey(record.ProposalSessionId!.Value));
        reply.Body.ShouldBe(AcceptanceTexts.Accepted(item.Key, "Sign in says Sign in"));
    }

    [Test]
    public async Task Accept_WhenTypeDiffers_ShouldReplaceAndRepoint()
    {
        var issue = await ArrangeAsync(Proposal("new", "feature", "Feature from an issue"));
        var triage = await ItemAsync(issue.WorkItemId);

        await AcceptAsync(issue);
        await Worker.RunNextAsync(CancellationToken.None);

        var record = await RecordAsync(issue.Id);
        record.WorkItemId.ShouldNotBe(triage.Id);
        record.Phase.ShouldBe(IssuePhase.Accepted);
        var made = await ItemAsync(record.WorkItemId);
        made.Type.ShouldBe("feature");
        made.Title.ShouldBe("Feature from an issue");
        made.StateName.ShouldBe(await AcceptedStateNameAsync("feature"));
        var links = await LinksAsync(made.Id);
        links.ShouldContain(l => l.Url == $"https://github.com/{_repository}/issues/{issue.Issue.Number}");
        links.ShouldContain(l => l.TargetId == triage.Id && l.Type == "related");
        (await ShipLeanCommentsAsync(made.Id)).ShouldHaveSingleItem().Body.ShouldBe(AcceptanceTexts.CameFrom(triage.Key));
        var removed = await ItemAsync(triage.Id);
        removed.Category.ShouldBe("removed");
        var note = (await ShipLeanCommentsAsync(triage.Id)).ShouldHaveSingleItem();
        note.Body.ShouldBe(AcceptanceTexts.Became(made.Key));
        note.AuthorId.ShouldBe(SeedIds.Users.Joel);
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().Body.ShouldBe(AcceptanceTexts.Accepted(made.Key, "Feature from an issue"));
    }

    [Test]
    public async Task Accept_WhenCrashAfterCreate_ShouldFinishWithSameItem()
    {
        var title = $"Crash after create {Guid.NewGuid():N}";
        var issue = await ArrangeAsync(Proposal("new", "bug", title));
        await AcceptAsync(issue);
        Store.CrashOnRepoint = true;

        await Worker.RunNextAsync(CancellationToken.None);

        (await ItemsTitledAsync(title)).ShouldBe(1);
        (await RecordAsync(issue.Id)).WorkItemId.ShouldBe(issue.WorkItemId);
        await MakeJobsDueAsync(issue.Id);
        (await Worker.RunNextAsync(CancellationToken.None)).ShouldBeTrue();

        (await ItemsTitledAsync(title)).ShouldBe(1);
        var record = await RecordAsync(issue.Id);
        record.Phase.ShouldBe(IssuePhase.Accepted);
        var made = await ItemAsync(record.WorkItemId);
        made.Title.ShouldBe(title);
        made.Type.ShouldBe("bug");
        (await ShipLeanCommentsAsync(issue.WorkItemId)).ShouldHaveSingleItem().Body.ShouldBe(AcceptanceTexts.Became(made.Key));
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Accept_WhenCrashBeforePhase_ShouldNotTriggerProgress()
    {
        var issue = await ArrangeAsync(Proposal("new", "story", "Crash before the phase"));
        await AcceptAsync(issue);
        Store.CrashOnAccepted = true;

        await Worker.RunNextAsync(CancellationToken.None);

        (await RecordAsync(issue.Id)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await Store.ListAcceptedOpenAsync()).ShouldNotContain(r => r.Id == issue.Id);
        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
        await MakeJobsDueAsync(issue.Id);
        await Worker.RunNextAsync(CancellationToken.None);

        (await RecordAsync(issue.Id)).Phase.ShouldBe(IssuePhase.Accepted);
        (await Store.ListAcceptedOpenAsync()).ShouldContain(r => r.Id == issue.Id);
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Accept_WhenDuplicate_ShouldRepointToExisting()
    {
        var existing = await CreateItemAsync(SeedIds.Projects.Ship, WorkItemTypeId.Story, "The existing story");
        var issue = await ArrangeAsync(Proposal("duplicate", "story", "Same as the existing story", existingKey: existing.Key));

        await AcceptAsync(issue);
        await Worker.RunNextAsync(CancellationToken.None);

        var record = await RecordAsync(issue.Id);
        record.WorkItemId.ShouldBe(existing.Id);
        record.Phase.ShouldBe(IssuePhase.Accepted);
        (await ItemAsync(issue.WorkItemId)).Category.ShouldBe("removed");
        (await ShipLeanCommentsAsync(issue.WorkItemId)).ShouldHaveSingleItem().Body.ShouldBe(AcceptanceTexts.DuplicateOf(existing.Key));
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().Body.ShouldBe(AcceptanceTexts.Duplicate(existing.Key, "The existing story"));
    }

    [Test]
    public async Task Accept_WhenAlreadyShipped_ShouldCloseIssue()
    {
        var issue = await ArrangeAsync(Proposal("alreadyShipped", "story", "Already there"), IssuePhase.AlreadyShipped);

        await AcceptAsync(issue);
        await Worker.RunNextAsync(CancellationToken.None);

        var record = await RecordAsync(issue.Id);
        record.Phase.ShouldBe(IssuePhase.Closed);
        record.WorkItemId.ShouldBe(issue.WorkItemId);
        (await ItemAsync(issue.WorkItemId)).Category.ShouldBe("removed");
        (await ShipLeanCommentsAsync(issue.WorkItemId)).ShouldHaveSingleItem().Body.ShouldBe(AcceptanceTexts.AlreadyShippedNote);
        var posts = await OutboxAsync(issue.Id);
        posts.Count.ShouldBe(2);
        posts[0].ShouldBe(new OutboxRow(AcceptanceTexts.ReplyKey(record.ProposalSessionId!.Value), "comment", AcceptanceTexts.Shipped, null));
        posts[1].ShouldBe(new OutboxRow(AcceptanceTexts.CloseKey(record.ProposalSessionId.Value), "close", null, "completed"));
    }

    [Test]
    public async Task Accept_WhenProposing_ShouldSayBusy()
    {
        var issue = await ArrangeAsync(Proposal("new", "story", "Busy"), IssuePhase.Proposing);

        (await AcceptAsync(issue, commentId: 77)).Outcome.ShouldBe(WebhookOutcome.Processed);
        await AcceptAsync(issue, commentId: 77);

        var busy = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        busy.DedupeKey.ShouldBe(AcceptanceTexts.BusyKey(77));
        busy.Body.ShouldBe(AcceptanceTexts.Busy);
        (await JobCountAsync(issue.Id)).ShouldBe(0);
        (await RecordAsync(issue.Id)).Phase.ShouldBe(IssuePhase.Proposing);
    }

    [Test]
    public async Task Accept_WhenNeedsInfo_ShouldSayQuestionsOpen()
    {
        var issue = await ArrangeAsync(Proposal("needsInfo", "story", "Which page?"));

        (await AcceptAsync(issue, commentId: 78)).Outcome.ShouldBe(WebhookOutcome.Processed);

        var reply = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        reply.DedupeKey.ShouldBe(AcceptanceTexts.QuestionsKey(78));
        reply.Body.ShouldBe(AcceptanceTexts.QuestionsOpen);
        (await JobCountAsync(issue.Id)).ShouldBe(0);
        (await RecordAsync(issue.Id)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await ItemAsync(issue.WorkItemId)).IsIntake.ShouldBeTrue();
    }

    [Test]
    public async Task Accept_WhenTwice_ShouldActOnce()
    {
        var title = $"Accepted twice {Guid.NewGuid():N}";
        var issue = await ArrangeAsync(Proposal("new", "feature", title));

        await AcceptAsync(issue, commentId: 1);
        await AcceptAsync(issue, commentId: 2);
        (await JobCountAsync(issue.Id)).ShouldBe(1);
        await Worker.RunNextAsync(CancellationToken.None);
        (await Worker.RunNextAsync(CancellationToken.None)).ShouldBeFalse();
        var after = await AcceptAsync(issue, commentId: 3);

        after.ShouldBe(IssueOutcome.Ignored);
        (await JobCountAsync(issue.Id)).ShouldBe(1);
        (await ItemsTitledAsync(title)).ShouldBe(1);
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Accept_WhenIssueClosed_ShouldDoNothing()
    {
        var issue = await ArrangeAsync(Proposal("new", "story", "Closed meanwhile"));
        (await Store.SetOpenOnGitHubAsync(issue.Id, false, DateTimeOffset.FromUnixTimeSeconds(1_900_000_000))).ShouldBeTrue();

        var outcome = await AcceptAsync(issue);

        outcome.ShouldBe(IssueOutcome.Ignored);
        (await JobCountAsync(issue.Id)).ShouldBe(0);
        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
        (await RecordAsync(issue.Id)).Phase.ShouldBe(IssuePhase.AwaitingReply);
    }

    [Test]
    public async Task Accept_WhenParentInOtherProject_ShouldIgnoreParent()
    {
        var elsewhere = await CreateItemAsync(SeedIds.Projects.Pay, WorkItemTypeId.Feature, "A feature of another project");
        var issue = await ArrangeAsync(Proposal("new", "story", "Parent elsewhere", parentKey: elsewhere.Key));

        await AcceptAsync(issue);
        await Worker.RunNextAsync(CancellationToken.None);

        var item = await ItemAsync(issue.WorkItemId);
        item.ParentId.ShouldBeNull();
        item.Title.ShouldBe("Parent elsewhere");
        (await RecordAsync(issue.Id)).Phase.ShouldBe(IssuePhase.Accepted);
    }

    private static string Proposal(string verdict, string type, string title, string? parentKey = null, string? existingKey = null, string[]? criteria = null)
        => JsonSerializer.Serialize(new
        {
            verdict,
            type,
            title,
            description = "The description.",
            acceptanceCriteria = criteria ?? ["It works"],
            parentKey,
            existing = existingKey is null ? Array.Empty<object>() : [new { key = existingKey }],
            requires = Array.Empty<string>(),
            questions = verdict == "needsInfo" ? ["Which page?"] : Array.Empty<string>(),
        });

    // Opens an issue through intake (a real Triage item), then stores the proposal and moves the phase as the proposal lane would.
    private async Task<GitHubIssueRecord> ArrangeAsync(string proposalJson, IssuePhase phase = IssuePhase.AwaitingReply)
    {
        var number = Interlocked.Increment(ref _number);
        var issueRef = new IssueRef(IssueRef.GitHubHost, _repository, number);
        var opened = new IssueEvent(
            IssueAction.Opened, issueRef, $"https://github.com/{_repository}/issues/{number}", $"Issue {number}", "A body",
            "reporter", "NONE", [], true, null, "reporter", false, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
        await using (var scope = Host.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IIssueIntake>().OpenedAsync(opened, CancellationToken.None)).Outcome.ShouldBe(WebhookOutcome.Processed);
        }

        var record = (await Store.FindAsync(issueRef)).ShouldNotBeNull();
        await Store.SaveProposalAsync(record.Id, await ProposeSessionAsync(record.WorkItemId), proposalJson);
        if (phase != IssuePhase.Proposing)
        {
            (await Store.SetPhaseAsync(record.Id, [IssuePhase.Proposing], phase)).ShouldBeTrue();
        }

        return await RecordAsync(record.Id);
    }

    // A finished propose session on the Triage item, as the proposal that is accepted was written by one.
    private async Task<Guid> ProposeSessionAsync(Guid workItemId)
    {
        var connection = Guid.NewGuid();
        var session = Guid.NewGuid();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            """
            INSERT INTO agent_connection (id, type, name, receiver_url, signing_secret_protected, signing_secret_last4, callback_token_hash, callback_token_last4, created_at, updated_at)
            VALUES (@connection, 'linear-compatible', @name, 'https://relay.example.test', 'x', 'xxxx', @hash, 'xxxx', now(), now());
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at, mode)
            VALUES (@session, @workItemId, @connection, 'completed', 'https://github.com/shiplean-tests/accept', 'main', now(), @joel, now(), 'propose');
            """,
            new { connection, session, workItemId, name = $"accept-{connection:N}", hash = connection.ToByteArray(), joel = SeedIds.Users.Joel });
        return session;
    }

    private async Task<IssueOutcome> AcceptAsync(GitHubIssueRecord issue, long commentId = 10)
    {
        var url = $"https://github.com/{_repository}/issues/{issue.Issue.Number}";
        var comment = new IssueCommentEvent(
            issue.Issue, url, issue.Title, "reporter", true, false, commentId, $"{url}#issuecomment-{commentId}", "LGTM", "reporter", "NONE", false,
            DateTimeOffset.FromUnixTimeSeconds(1_800_000_100));
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIssueAcceptance>().AcceptAsync(comment, CancellationToken.None);
    }

    private async Task<(Guid Id, string Key)> CreateItemAsync(Guid projectId, WorkItemTypeId type, string title)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var written = (await scope.ServiceProvider.GetRequiredService<IWorkItemStore>()
            .CreateAsync(new WorkItemCreate(projectId, type, title, null, null, null), WriteOptions.FromWebhook("GitHub tests"))).Written!;
        return (written.Id, written.Key);
    }

    private async Task<GitHubIssueRecord> RecordAsync(Guid id) => (await Store.FindByIdAsync(id)).ShouldNotBeNull();

    private async Task MakeJobsDueAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "UPDATE issue_job SET next_attempt_at = next_attempt_at - interval '1 day', leased_until = NULL WHERE github_issue_id = @issueId AND status = 'pending'",
            new { issueId });
    }

    private async Task<string> AcceptedStateNameAsync(string type)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<string>(
            "SELECT name FROM state WHERE project_id = @project AND type = @type AND category = 'proposed' AND NOT is_intake AND archived_at IS NULL ORDER BY sort_order LIMIT 1",
            new { project = SeedIds.Projects.Ship, type });
    }

    private async Task<ItemRow> ItemAsync(Guid id)
    {
        await using var database = await OpenDatabaseAsync();
        var row = await database.QuerySingleAsync<ItemRow>(
            """
            SELECT w.id, 'SHIP-' || w.number AS key, w.type, w.title, w.description, s.name AS stateName, s.category, s.is_intake AS isIntake,
                   w.parent_id AS parentId, w.acceptance_criteria::text AS criteria
            FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.id = @id
            """,
            new { id });
        return row;
    }

    private async Task<int> ItemsTitledAsync(string title)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE title = @title", new { title });
    }

    private async Task<List<LinkRow>> LinksAsync(Guid itemId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<LinkRow>(
            "SELECT target_id AS targetId, link_type AS type, url FROM work_item_link WHERE source_id = @itemId AND archived_at IS NULL", new { itemId })];
    }

    private async Task<List<OutboxRow>> OutboxAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<OutboxRow>(
            "SELECT dedupe_key AS dedupeKey, kind, body, reason FROM github_outbox WHERE github_issue_id = @issueId ORDER BY seq", new { issueId })];
    }

    private async Task<int> JobCountAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<int>("SELECT count(*)::int FROM issue_job WHERE github_issue_id = @issueId AND kind = 'accept'", new { issueId });
    }

    private async Task<List<CommentRow>> ShipLeanCommentsAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<CommentRow>(
            "SELECT author_id AS authorId, body FROM comment WHERE work_item_id = @workItemId AND agent_name = @AgentName ORDER BY created_at",
            new { workItemId, RoundHolds.AgentName })];
    }

    private sealed record ItemRow(Guid Id, string Key, string Type, string Title, string Description, string StateName, string Category, bool IsIntake, Guid? ParentId, string Criteria);

    private sealed record LinkRow(Guid? TargetId, string Type, string? Url);

    private sealed record OutboxRow(string DedupeKey, string Kind, string? Body, string? Reason);

    private sealed record CommentRow(Guid AuthorId, string Body);

    // The real store, crashing once on request: on a repoint (after the create committed) or on the move to accepted (the last step).
    private sealed class CrashingIssueStore(IGitHubIssueStore inner) : IGitHubIssueStore
    {
        public bool CrashOnRepoint { get; set; }

        public bool CrashOnAccepted { get; set; }

        public Task<GitHubIssueRecord?> FindAsync(IssueRef issue, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.FindAsync(issue, transaction, cancellationToken);

        public Task<GitHubIssueRecord?> FindByIdAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.FindByIdAsync(id, transaction, cancellationToken);

        public Task<GitHubIssueRecord?> FindByWorkItemAsync(Guid workItemId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.FindByWorkItemAsync(workItemId, transaction, cancellationToken);

        public Task<GitHubIssueRecord> InsertAsync(
            IssueRef issue, Guid projectId, Guid workItemId, string authorLogin, string title, string body, IssuePhase phase, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.InsertAsync(issue, projectId, workItemId, authorLogin, title, body, phase, transaction, cancellationToken);

        public Task<bool> UpdateTextAsync(Guid id, string title, string body, DateTimeOffset eventUpdatedAt, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.UpdateTextAsync(id, title, body, eventUpdatedAt, transaction, cancellationToken);

        public Task<bool> SetPhaseAsync(Guid id, IReadOnlyList<IssuePhase> from, IssuePhase to, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (CrashOnAccepted && to == IssuePhase.Accepted)
            {
                CrashOnAccepted = false;
                throw new InvalidOperationException("crash before the phase");
            }

            return inner.SetPhaseAsync(id, from, to, transaction, cancellationToken);
        }

        public Task SaveProposalAsync(Guid id, Guid sessionId, string proposalJson, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.SaveProposalAsync(id, sessionId, proposalJson, transaction, cancellationToken);

        public Task<int> IncrementRoundsAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.IncrementRoundsAsync(id, transaction, cancellationToken);

        public Task RepointAsync(Guid id, Guid workItemId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (CrashOnRepoint)
            {
                CrashOnRepoint = false;
                throw new InvalidOperationException("crash after the create");
            }

            return inner.RepointAsync(id, workItemId, transaction, cancellationToken);
        }

        public Task<bool> SetOpenOnGitHubAsync(Guid id, bool open, DateTimeOffset eventUpdatedAt, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.SetOpenOnGitHubAsync(id, open, eventUpdatedAt, transaction, cancellationToken);

        public Task<IReadOnlyList<GitHubIssueRecord>> ListAcceptedOpenAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.ListAcceptedOpenAsync(transaction, cancellationToken);

        public Task MarkPostedAsync(Guid id, string milestone, string value, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => inner.MarkPostedAsync(id, milestone, value, transaction, cancellationToken);
    }
}
