using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.IssueIntake.Records;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.IssueIntake.AgentMode;

// Propose-mode agent sessions (C4): the stored mode, the allowlisted context a propose session gets (D10, D17), the comments it
// holds (only those with GitHub provenance), and the proposal job a propose session's response enqueues in the activity's own transaction.
[TestFixture]
public class AgentModeTests : ApiFixture
{
    private const string GitHubComment = "Mirrored from GitHub: it fails on Firefox too.";
    private const string AgentComment = "An agent note on the item.";
    private const string PersonComment = "A maintainer private note.";
    private const string ImpersonatingComment = "Posted by an agent that calls itself GitHub.";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<DbIssueJobs>();
        services.AddSingleton<RollbackAfterEnqueueJobs>();
        services.AddSingleton<IIssueJobs>(sp => sp.GetRequiredService<RollbackAfterEnqueueJobs>());
    };

    private AgentSessionStarter Starter => Host.Services.GetRequiredService<AgentSessionStarter>();

    private RollbackAfterEnqueueJobs Jobs => Host.Services.GetRequiredService<RollbackAfterEnqueueJobs>();

    [TearDown]
    public void EnqueueNormally() => Jobs.FailAfterEnqueue = false;

    [Test]
    public async Task Starter_WhenModeOmitted_ShouldStoreImplement()
    {
        var (_, item) = await ArrangeItemAsync();

        var outcome = await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None);

        (await ModeAsync(outcome.SessionId!.Value)).ShouldBe("implement");
        (await CreatedBodyAsync(item)).AsObject().ContainsKey("shipleanMode").ShouldBeFalse();
    }

    [Test]
    public async Task Starter_WhenPropose_ShouldStoreProposeAndSayItInCreated()
    {
        var (arranged, item) = await ArrangeItemAsync();
        await InsertIssueAsync(arranged, item);

        var outcome = await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None, AgentSessionMode.Propose);

        (await ModeAsync(outcome.SessionId!.Value)).ShouldBe("propose");
        (await CreatedBodyAsync(item))["shipleanMode"]!.GetValue<string>().ShouldBe("propose");
    }

    [Test]
    public async Task Context_WhenPropose_ShouldOnlyIncludeGitHubComments()
    {
        var (arranged, item) = await ArrangeItemAsync();
        var issue = await InsertIssueAsync(arranged, item);
        await AddCommentsAsync(item);
        await MirrorAsync(issue, GitHubComment);

        await Starter.StartWinnerAsync(item, SeedIds.Users.Joel, null, null, (_, _) => Task.FromResult("Propose."), CancellationToken.None, AgentSessionMode.Propose);

        var context = (await CreatedBodyAsync(item))["promptContext"]!.GetValue<string>();
        context.ShouldContain(GitHubComment);
        context.ShouldContain("<comment author-login=\"reporter\"");
        context.ShouldNotContain(AgentComment);
        context.ShouldNotContain(PersonComment);
        context.ShouldNotContain("Joel Dickson");
    }

    [Test]
    public async Task Context_WhenAgentNameImpersonatesGitHub_ShouldLeaveCommentOut()
    {
        var (arranged, item) = await ArrangeItemAsync();
        await InsertIssueAsync(arranged, item);
        await using (var scope = Host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICommentStore>()
                .AddAsAgentAsync(item, new CommentCreate(ImpersonatingComment), "GitHub @reporter", SeedIds.Users.Joel);
        }

        await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None, AgentSessionMode.Propose);

        var context = (await CreatedBodyAsync(item))["promptContext"]!.GetValue<string>();
        context.ShouldNotContain(ImpersonatingComment);
        context.ShouldNotContain("<github-comments>");
    }

    [Test]
    public async Task Created_WhenPropose_ShouldCarryNoPrivateTextAnywhere()
    {
        var (arranged, item) = await ArrangeItemAsync();
        string[] sentinels = await ArrangePrivateTextAsync(arranged, item);
        var fallback = "https://private-instance-sentinel.example";
        string instance;
        await using (var database = await OpenDatabaseAsync())
        {
            var stored = await database.QuerySingleOrDefaultAsync<string?>("SELECT base_url FROM instance_settings");
            instance = string.IsNullOrWhiteSpace(stored) ? fallback : stored.TrimEnd('/');
        }

        // Control: an implement session on the same item carries every sentinel, so each one is really reachable.
        var implement = (await Starter.StartAsync(item, SeedIds.Users.Joel, fallback, null, null, null, CancellationToken.None)).SessionId!.Value;
        var implementBody = await CreatedOfModeAsync(item, "implement");
        foreach (var sentinel in sentinels.Append(instance))
        {
            implementBody.ShouldContain(sentinel);
        }
        await EndAsync(implement);

        var issue = await InsertIssueAsync(arranged, item);
        await MirrorAsync(issue, GitHubComment);
        await Starter.StartWinnerAsync(
            item, SeedIds.Users.Joel, fallback, null, (_, _) => Task.FromResult("Propose."), CancellationToken.None, AgentSessionMode.Propose,
            [new AgentRelatedWork("SHIP-12", "story", "Done", "Related title", ["https://github.com/o/r/pull/4"], [new AgentMergeCommit("abc1234", null)])]);

        var body = await CreatedOfModeAsync(item, "propose");
        foreach (var sentinel in sentinels.Append(instance))
        {
            body.ShouldNotContain(sentinel);
        }
        body.ShouldContain(GitHubComment);
        body.ShouldContain("SHIP-12");
        body.ShouldContain("Related title");
        var created = JsonNode.Parse(body)!;
        var session = created["agentSession"]!;
        session["issue"]!["title"]!.GetValue<string>().ShouldBe(issue.Title);
        session["issue"]!["description"]!.GetValue<string>().ShouldBe(issue.Body);
        session["issue"]!["url"]!.GetValue<string>().ShouldBeEmpty();
        session["issue"]!["labels"]!.AsArray().ShouldBeEmpty();
        session["creator"]!["name"]!.GetValue<string>().ShouldBeEmpty();
        session["creator"]!["email"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Starter_WhenProposeOnItemWithoutGitHubIssue_ShouldRefuse()
    {
        var (_, item) = await ArrangeItemAsync();

        var refused = await Should.ThrowAsync<ApiException>(
            () => Starter.StartWinnerAsync(item, SeedIds.Users.Joel, null, null, (_, _) => Task.FromResult("Propose."), CancellationToken.None, AgentSessionMode.Propose));

        refused.Code.ShouldBe(ApiErrorCodes.Validation);
        await using var database = await OpenDatabaseAsync();
        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Context_WhenImplement_ShouldIncludeAllComments()
    {
        var (_, item) = await ArrangeItemAsync();
        await AddCommentsAsync(item);

        await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None);

        var context = (await CreatedBodyAsync(item))["promptContext"]!.GetValue<string>();
        context.ShouldContain(GitHubComment);
        context.ShouldContain(AgentComment);
        context.ShouldContain(PersonComment);
        context.ShouldNotContain("author=\"GitHub @reporter\"");
    }

    [Test]
    public async Task Response_WhenProposeSession_ShouldEnqueueJobInSameTransaction()
    {
        var (arranged, item) = await ArrangeItemAsync();
        var issue = await InsertIssueAsync(arranged, item);
        var sessionId = (await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None, AgentSessionMode.Propose)).SessionId!.Value;

        Jobs.FailAfterEnqueue = true;
        await Should.ThrowAsync<InvalidOperationException>(() => RespondAsync(arranged, sessionId, "The first proposal."));
        (await JobsAsync(issue.Id)).ShouldBeEmpty();
        (await ActivityCountAsync(sessionId)).ShouldBe(0);

        Jobs.FailAfterEnqueue = false;
        await RespondAsync(arranged, sessionId, "The proposal.\n\n```shiplean-proposal\n{}\n```");

        var job = (await JobsAsync(issue.Id)).ShouldHaveSingleItem();
        job.Kind.ShouldBe("proposal");
        job.Key.ShouldBe($"proposal:{sessionId}");
        job.ProjectId.ShouldBe(arranged.Project.Id);
        job.Status.ShouldBe("pending");
        var payload = JsonNode.Parse(job.Payload)!;
        payload["sessionId"]!.GetValue<Guid>().ShouldBe(sessionId);
        payload["body"]!.GetValue<string>().ShouldBe("The proposal.\n\n```shiplean-proposal\n{}\n```");
        (await ActivityCountAsync(sessionId)).ShouldBe(1);
    }

    [Test]
    public async Task Response_WhenImplementSession_ShouldNotEnqueue()
    {
        var (arranged, item) = await ArrangeItemAsync();
        var issue = await InsertIssueAsync(arranged, item);
        var sessionId = (await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None)).SessionId!.Value;

        await RespondAsync(arranged, sessionId, "Done: opened a pull request.");

        (await JobsAsync(issue.Id)).ShouldBeEmpty();
        (await ActivityCountAsync(sessionId)).ShouldBe(1);
    }

    [Test]
    public async Task Thought_WhenProposeSession_ShouldNotEnqueue()
    {
        var (arranged, item) = await ArrangeItemAsync();
        var issue = await InsertIssueAsync(arranged, item);
        var sessionId = (await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None, AgentSessionMode.Propose)).SessionId!.Value;

        await ReportAsync(arranged, sessionId, "thought", "Reading the backlog code.");

        (await JobsAsync(issue.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Response_WhenProposeItemHasNoGitHubIssue_ShouldCommitAndFailSession()
    {
        var (arranged, item) = await ArrangeItemAsync();
        var issue = await InsertIssueAsync(arranged, item);
        var sessionId = (await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None, AgentSessionMode.Propose)).SessionId!.Value;
        var other = (await arranged.CreateItemAsync(Api)).Id;
        await Host.Services.GetRequiredService<IGitHubIssueStore>().RepointAsync(issue.Id, other);

        await RespondAsync(arranged, sessionId, "A proposal for an item that lost its issue.");

        (await ActivityCountAsync(sessionId)).ShouldBe(1);
        (await JobsAsync(issue.Id)).ShouldBeEmpty();
        await using var database = await OpenDatabaseAsync();
        (await database.QuerySingleAsync<string>("SELECT state FROM agent_session WHERE id = @sessionId", new { sessionId })).ShouldBe("failed");
    }

    [Test]
    public async Task Response_WhenReplayed_ShouldKeepOneActivityAndOneJob()
    {
        var (arranged, item) = await ArrangeItemAsync();
        var issue = await InsertIssueAsync(arranged, item);
        var sessionId = (await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None, AgentSessionMode.Propose)).SessionId!.Value;
        var activityId = Guid.CreateVersion7();

        var first = await ReportAsync(arranged, sessionId, "response", "The proposal.", activityId);
        var second = await ReportAsync(arranged, sessionId, "response", "The proposal.", activityId);

        second.ShouldBe(first);
        (await ActivityCountAsync(sessionId)).ShouldBe(1);
        (await JobsAsync(issue.Id)).ShouldHaveSingleItem();
    }

    private async Task<(AgentDispatchArrange Arranged, Guid Item)> ArrangeItemAsync()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        return (arranged, (await arranged.CreateItemAsync(Api)).Id);
    }

    private async Task AddCommentsAsync(Guid item)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var comments = scope.ServiceProvider.GetRequiredService<ICommentStore>();
        await comments.AddAsAgentAsync(item, new CommentCreate(GitHubComment), "GitHub @reporter", SeedIds.Users.Joel);
        await comments.AddAsAgentAsync(item, new CommentCreate(AgentComment), "Relay", SeedIds.Users.Joel);
        using var posted = await Api.PostJsonAsync($"api/work-items/{item}/comments", new { body = PersonComment });
        posted.EnsureSuccessStatusCode();
    }

    private Task<GitHubIssueRecord> InsertIssueAsync(AgentDispatchArrange arranged, Guid item)
        => Host.Services.GetRequiredService<IGitHubIssueStore>().InsertAsync(
            new IssueRef(IssueRef.GitHubHost, "shiplean-tests/agent-mode", Random.Shared.Next(1, int.MaxValue)), arranged.Project.Id, item, "reporter",
            "Login button says Sing in", "The login page says Sing in.", IssuePhase.Proposing);

    // A GitHub comment mirrored with its provenance row, as the replies lane does it.
    private async Task MirrorAsync(GitHubIssueRecord issue, string body)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        await using var database = await OpenDatabaseAsync();
        await using var transaction = await database.BeginTransactionAsync();
        var githubId = Random.Shared.NextInt64(1, long.MaxValue);
        await scope.ServiceProvider.GetRequiredService<IMirroredComments>().AddAsync(
            new MirroredComment(issue.Id, githubId, $"https://github.com/{issue.Issue.Repository}/issues/{issue.Issue.Number}#issuecomment-{githubId}", "reporter", body, Host.Services.GetRequiredService<TimeProvider>().GetUtcNow(), SeedIds.Users.Joel),
            transaction);
        await transaction.CommitAsync();
    }

    // Private text in every place a propose session must not read: the item's description, a parent's description, an attachment's
    // name, a link, a ShipLean-only comment and the creator's name. Returns the sentinels.
    private async Task<string[]> ArrangePrivateTextAsync(AgentDispatchArrange arranged, Guid item)
    {
        var parent = (await arranged.CreateItemAsync(Api, type: "feature")).Id;
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE work_item SET description = 'private-item-description-sentinel', parent_id = @parent, parent_type = 'feature' WHERE id = @item", new { item, parent });
        await database.ExecuteAsync("UPDATE work_item SET description = 'private-parent-description-sentinel' WHERE id = @parent", new { parent });
        await database.ExecuteAsync(
            """
            INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref, uploaded_by, created_at)
            VALUES (@id, @item, 'private-attachment-sentinel.png', 'image/png', 10, 'private/ref', @joel, now())
            """,
            new { id = Guid.NewGuid(), item, joel = SeedIds.Users.Joel });
        await database.ExecuteAsync(
            """
            INSERT INTO work_item_link (id, source_id, link_type, url, title, created_by, created_at)
            VALUES (@id, @item, 'url', 'https://private-link-sentinel.example/doc', 'Private link title', @joel, now())
            """,
            new { id = Guid.NewGuid(), item, joel = SeedIds.Users.Joel });
        await using (var scope = Host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICommentStore>().AddAsAgentAsync(item, new CommentCreate("private-comment-sentinel"), "Relay", SeedIds.Users.Joel);
        }
        var creatorName = await database.QuerySingleAsync<string>("SELECT name FROM app_user WHERE id = @joel", new { joel = SeedIds.Users.Joel });
        return ["private-item-description-sentinel", "private-parent-description-sentinel", "private-attachment-sentinel", "private-link-sentinel", "private-comment-sentinel", creatorName];
    }

    private Task RespondAsync(AgentDispatchArrange arranged, Guid sessionId, string body) => ReportAsync(arranged, sessionId, "response", body);

    private async Task<Guid> ReportAsync(AgentDispatchArrange arranged, Guid sessionId, string type, string body, Guid? activityId = null)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAgentReports>().CreateActivityAsync(
            new AgentReporter(arranged.ConnectionId!.Value, "Relay"),
            new AgentActivityCreate(sessionId, type, body, null, null, null, false, null, activityId));
    }

    private async Task<string> CreatedOfModeAsync(Guid item, string mode)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<string>(
            """
            SELECT d.body::text FROM agent_delivery d JOIN agent_session s ON s.id = d.session_id
            WHERE s.work_item_id = @item AND s.mode = @mode AND d.action = 'created'
            """,
            new { item, mode });
    }

    private async Task EndAsync(Guid sessionId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE agent_session SET state = 'completed', ended_at = now() WHERE id = @sessionId", new { sessionId });
    }

    private async Task<string> ModeAsync(Guid sessionId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<string>("SELECT mode FROM agent_session WHERE id = @sessionId", new { sessionId });
    }

    private async Task<JsonNode> CreatedBodyAsync(Guid item)
    {
        await using var database = await OpenDatabaseAsync();
        return AgentDispatchArrange.BodyOf((await AgentDispatchArrange.DeliveriesAsync(database, item)).ShouldHaveSingleItem());
    }

    private async Task<List<JobRow>> JobsAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<JobRow>(
            "SELECT kind, key, project_id AS projectId, status, payload::text AS payload FROM issue_job WHERE github_issue_id = @issueId", new { issueId })];
    }

    private async Task<int> ActivityCountAsync(Guid sessionId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<int>("SELECT count(*) FROM agent_activity WHERE session_id = @sessionId", new { sessionId });
    }

    private sealed record JobRow(string Kind, string Key, Guid ProjectId, string Status, string Payload);
}
