using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.IssueIntake.Records;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.IssueIntake.AgentMode;

// Propose-mode agent sessions (C4): the stored mode, the comments a propose session's context holds, and the proposal job a
// propose session's response enqueues in the activity's own transaction.
[TestFixture]
public class AgentModeTests : ApiFixture
{
    private const string GitHubComment = "Mirrored from GitHub: it fails on Firefox too.";
    private const string AgentComment = "An agent's note on the item.";
    private const string PersonComment = "A maintainer's private note.";

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
        var (_, item) = await ArrangeItemAsync();

        var outcome = await Starter.StartAsync(item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None, AgentSessionMode.Propose);

        (await ModeAsync(outcome.SessionId!.Value)).ShouldBe("propose");
        (await CreatedBodyAsync(item))["shipleanMode"]!.GetValue<string>().ShouldBe("propose");
    }

    [Test]
    public async Task Context_WhenPropose_ShouldOnlyIncludeGitHubComments()
    {
        var (_, item) = await ArrangeItemAsync();
        await AddCommentsAsync(item);

        await Starter.StartWinnerAsync(item, SeedIds.Users.Joel, null, null, (_, _) => Task.FromResult("Propose."), CancellationToken.None, AgentSessionMode.Propose);

        var context = (await CreatedBodyAsync(item))["promptContext"]!.GetValue<string>();
        context.ShouldContain(GitHubComment);
        context.ShouldContain("author=\"GitHub @reporter\"");
        context.ShouldNotContain(AgentComment);
        context.ShouldNotContain(PersonComment);
        context.ShouldNotContain("Joel Dickson");
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

    private async Task<(AgentDispatchArrange Arranged, Guid Item)> ArrangeItemAsync()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        return (arranged, (await arranged.CreateItemAsync(Api)).Id);
    }

    private async Task AddCommentsAsync(Guid item)
    {
        var comments = Host.Services.GetRequiredService<ICommentStore>();
        await comments.AddAsAgentAsync(item, new CommentCreate(GitHubComment), "GitHub @reporter", SeedIds.Users.Joel);
        await comments.AddAsAgentAsync(item, new CommentCreate(AgentComment), "Relay", SeedIds.Users.Joel);
        using var posted = await Api.PostJsonAsync($"api/work-items/{item}/comments", new { body = PersonComment });
        posted.EnsureSuccessStatusCode();
    }

    private Task<GitHubIssueRecord> InsertIssueAsync(AgentDispatchArrange arranged, Guid item)
        => Host.Services.GetRequiredService<IGitHubIssueStore>().InsertAsync(
            new IssueRef(IssueRef.GitHubHost, "shiplean-tests/agent-mode", Random.Shared.Next(1, int.MaxValue)), arranged.Project.Id, item, "reporter", IssuePhase.Proposing);

    private Task RespondAsync(AgentDispatchArrange arranged, Guid sessionId, string body) => ReportAsync(arranged, sessionId, "response", body);

    private async Task ReportAsync(AgentDispatchArrange arranged, Guid sessionId, string type, string body)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAgentReports>().CreateActivityAsync(
            new AgentReporter(arranged.ConnectionId!.Value, "Relay"),
            new AgentActivityCreate(sessionId, type, body, null, null, null, false, null, null));
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
