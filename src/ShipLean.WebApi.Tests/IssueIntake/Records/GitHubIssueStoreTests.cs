using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.IssueIntake.Records;

// The real github_issue store and project_issue_intake table on a seeded clone. Each test takes its own issue number.
[TestFixture]
public class GitHubIssueStoreTests : ApiFixture
{
    private const string Repository = "shiplean-tests/store";

    private static int _number;

    private IGitHubIssueStore Store => Host.Services.GetRequiredService<IGitHubIssueStore>();

    private static IssueRef NextIssue() => new(IssueRef.GitHubHost, Repository, Interlocked.Increment(ref _number));

    private Task<GitHubIssueRecord> InsertAsync(IssueRef issue, IssuePhase phase = IssuePhase.Proposing)
        => Store.InsertAsync(issue, SeedIds.Projects.Ship, SeedIds.WorkItems.Ship89, "reporter", "The title", "The body", phase);

    [Test]
    public async Task Insert_WhenSameIssueTwice_ShouldReturnOneRecord()
    {
        var issue = NextIssue();

        var first = await InsertAsync(issue);
        var second = await Store.InsertAsync(issue, SeedIds.Projects.Ship, SeedIds.WorkItems.Ship90, "someone-else", "Another title", "Another body", IssuePhase.Accepted);

        (second with { LastPosted = first.LastPosted }).ShouldBe(first);
        second.LastPosted.ShouldBeEmpty();
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM github_issue WHERE repository = @Repository AND number = @Number", new { issue.Repository, issue.Number })).ShouldBe(1);
    }

    [Test]
    public async Task UpdateText_WhenIssueEdited_ShouldReplaceTitleAndBodyOnly()
    {
        var issue = NextIssue();
        var record = await InsertAsync(issue);

        await Store.UpdateTextAsync(record.Id, "The edited title", "The edited body", DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));

        var found = (await Store.FindByIdAsync(record.Id))!;
        found.Title.ShouldBe("The edited title");
        found.Body.ShouldBe("The edited body");
        (found with { Title = record.Title, Body = record.Body, LastPosted = record.LastPosted, UpdatedAt = record.UpdatedAt }).ShouldBe(record);
    }

    [Test]
    public async Task Insert_WhenNew_ShouldStoreTheIssue()
    {
        var issue = NextIssue();

        var record = await InsertAsync(issue);

        var found = (await Store.FindAsync(issue))!;
        (found with { LastPosted = record.LastPosted }).ShouldBe(record);
        record.ShouldSatisfyAllConditions(
            () => record.Issue.ShouldBe(issue),
            () => record.ProjectId.ShouldBe(SeedIds.Projects.Ship),
            () => record.WorkItemId.ShouldBe(SeedIds.WorkItems.Ship89),
            () => record.AuthorLogin.ShouldBe("reporter"),
            () => record.Phase.ShouldBe(IssuePhase.Proposing),
            () => record.Rounds.ShouldBe(0),
            () => record.OpenOnGitHub.ShouldBeTrue(),
            () => record.LastPosted.ShouldBeEmpty());
    }

    [Test]
    public async Task SetPhase_WhenFromDoesNotMatch_ShouldReturnFalse()
    {
        var record = await InsertAsync(NextIssue(), IssuePhase.Proposing);

        var moved = await Store.SetPhaseAsync(record.Id, [IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped], IssuePhase.Accepted);

        moved.ShouldBeFalse();
        (await Store.FindByIdAsync(record.Id))!.Phase.ShouldBe(IssuePhase.Proposing);
    }

    [Test]
    public async Task SetPhase_WhenFromMatches_ShouldMoveAndReturnTrue()
    {
        var record = await InsertAsync(NextIssue(), IssuePhase.AwaitingReply);

        var moved = await Store.SetPhaseAsync(record.Id, [IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped], IssuePhase.Accepted);

        moved.ShouldBeTrue();
        (await Store.FindByIdAsync(record.Id))!.Phase.ShouldBe(IssuePhase.Accepted);
    }

    [Test]
    public async Task IncrementRounds_WhenConcurrent_ShouldCountEach()
    {
        var record = await InsertAsync(NextIssue());

        var counts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Store.IncrementRoundsAsync(record.Id)));

        counts.Order().ShouldBe([1, 2, 3, 4, 5, 6, 7, 8]);
        (await Store.FindByIdAsync(record.Id))!.Rounds.ShouldBe(8);
    }

    [Test]
    public async Task Insert_WhenTransactionRollsBack_ShouldNotKeepTheIssue()
    {
        var issue = NextIssue();
        var dataSource = Host.Services.GetRequiredService<NpgsqlDataSource>();
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await Store.InsertAsync(issue, SeedIds.Projects.Ship, SeedIds.WorkItems.Ship89, "reporter", "Title", "Body", IssuePhase.Proposing, transaction);
            (await Store.FindAsync(issue, transaction)).ShouldNotBeNull();
            await transaction.RollbackAsync();
        }

        (await Store.FindAsync(issue)).ShouldBeNull();
    }

    [Test]
    public async Task Updates_WhenApplied_ShouldChangeOnlyTheirFields()
    {
        var record = await InsertAsync(NextIssue());
        var sessionId = await CreateSessionAsync();

        await Store.SaveProposalAsync(record.Id, sessionId, """{"verdict":"new"}""");
        await Store.RepointAsync(record.Id, SeedIds.WorkItems.Ship91);
        await Store.SetOpenOnGitHubAsync(record.Id, false, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
        await Store.MarkPostedAsync(record.Id, "sprint", "Sprint 2");
        await Store.MarkPostedAsync(record.Id, "started", "Active");

        var stored = (await Store.FindByIdAsync(record.Id))!;
        stored.ShouldSatisfyAllConditions(
            () => stored.Proposal.ShouldBe("""{"verdict": "new"}"""),
            () => stored.ProposalSessionId.ShouldBe(sessionId),
            () => stored.WorkItemId.ShouldBe(SeedIds.WorkItems.Ship91),
            () => stored.OpenOnGitHub.ShouldBeFalse(),
            () => stored.LastPosted.ShouldBe(new Dictionary<string, string> { ["sprint"] = "Sprint 2", ["started"] = "Active" }, ignoreOrder: true),
            () => stored.Phase.ShouldBe(IssuePhase.Proposing));
        (await Store.FindByWorkItemAsync(SeedIds.WorkItems.Ship91))!.Id.ShouldBe(record.Id);
    }

    [Test]
    public async Task ListAcceptedOpen_WhenMixed_ShouldListOnlyAcceptedAndOpen()
    {
        var accepted = await InsertAsync(NextIssue(), IssuePhase.Accepted);
        var closedOnGitHub = await InsertAsync(NextIssue(), IssuePhase.Accepted);
        await Store.SetOpenOnGitHubAsync(closedOnGitHub.Id, false, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
        var proposing = await InsertAsync(NextIssue(), IssuePhase.Proposing);

        var listed = (await Store.ListAcceptedOpenAsync()).Select(r => r.Id).ToList();

        listed.ShouldContain(accepted.Id);
        listed.ShouldNotContain(closedOnGitHub.Id);
        listed.ShouldNotContain(proposing.Id);
    }

    [Test]
    public async Task EnabledRepository_WhenTwoProjects_ShouldViolateIndex()
    {
        await using var connection = await OpenDatabaseAsync();
        await EnableIntakeAsync(connection, SeedIds.Projects.Ship, "ShipLean/Intake", enabled: true);

        var error = await Should.ThrowAsync<PostgresException>(() => EnableIntakeAsync(connection, SeedIds.Projects.Pay, "shiplean/intake", enabled: true));

        error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Test]
    public async Task EnabledRepository_WhenTheOtherProjectIsOff_ShouldAllowBoth()
    {
        await using var connection = await OpenDatabaseAsync();
        await EnableIntakeAsync(connection, SeedIds.Projects.Mob, "ShipLean/Shared", enabled: false);

        await EnableIntakeAsync(connection, SeedIds.Projects.Data, "ShipLean/Shared", enabled: true);

        (await connection.ExecuteScalarAsync<int>("SELECT max_rounds FROM project_issue_intake WHERE project_id = @id", new { id = SeedIds.Projects.Data })).ShouldBe(5);
    }

    private static Task EnableIntakeAsync(NpgsqlConnection connection, Guid projectId, string repository, bool enabled)
        => connection.ExecuteAsync(
            "INSERT INTO project_issue_intake (project_id, enabled, repository) VALUES (@projectId, @enabled, @repository)",
            new { projectId, enabled, repository });

    // proposal_session_id references agent_session, and the seed has none: one connection and one ended session.
    private async Task<Guid> CreateSessionAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var connectionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO agent_connection (id, type, name, receiver_url, signing_secret_protected, signing_secret_last4, callback_token_hash, callback_token_last4, created_at, updated_at)
            VALUES (@connectionId, 'linear-compatible', @name, 'https://relay.example.com/hook', 'x', 'xxxx', @hash, 'xxxx', now(), now());
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at)
            VALUES (@sessionId, @itemId, @connectionId, 'completed', 'https://github.com/ShipLean/ShipLean.git', 'main', now(), @joel, now());
            """,
            new { connectionId, sessionId, name = $"Issue relay {connectionId:N}", hash = connectionId.ToByteArray(), itemId = SeedIds.WorkItems.Ship89, joel = SeedIds.Users.Joel });
        return sessionId;
    }

    [Test]
    public async Task SessionMode_WhenNotGiven_ShouldBeImplement()
    {
        var sessionId = await CreateSessionAsync();
        await using var connection = await OpenDatabaseAsync();

        (await connection.ExecuteScalarAsync<string>("SELECT mode FROM agent_session WHERE id = @sessionId", new { sessionId })).ShouldBe("implement");
    }

    [Test]
    public async Task IntakeState_WhenNotGiven_ShouldBeFalse()
    {
        await using var connection = await OpenDatabaseAsync();

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM state WHERE is_intake")).ShouldBe(0);
    }

    [Test]
    public async Task LinkState_WhenMerged_ShouldStoreMergeCommit()
    {
        await using var connection = await OpenDatabaseAsync();
        var mergedAt = TimeProvider.System.GetUtcNow();

        await connection.ExecuteAsync(
            """
            INSERT INTO link_state (url_key, url, state, as_of, source, merge_commit_sha, merged_at)
            VALUES ('github.com/shiplean/shiplean/pull/9999', 'https://github.com/ShipLean/ShipLean/pull/9999', 'merged', now(), 'github', @sha, @mergedAt)
            """,
            new { sha = "0123456789abcdef0123456789abcdef01234567", mergedAt });

        (await connection.ExecuteScalarAsync<string>("SELECT merge_commit_sha FROM link_state WHERE url_key = 'github.com/shiplean/shiplean/pull/9999'"))
            .ShouldBe("0123456789abcdef0123456789abcdef01234567");
    }
}
