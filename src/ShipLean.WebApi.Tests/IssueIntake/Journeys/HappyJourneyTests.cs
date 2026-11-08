using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Acceptance;
using ShipLean.Application.Features.IssueIntake.Inbox;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Application.Features.IssueIntake.Progress;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Application.Features.IssueIntake.ShippedIn;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Webhooks;
using ShipLean.TestSupport;
using ShipLean.TestSupport.IssueIntake;
using ShipLean.TestSupport.Webhooks;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.Links.GitHub;
using ShipLean.WebApi.Tests.Webhooks;

namespace ShipLean.WebApi.Tests.IssueIntake.Journeys;

// The happy paths across every lane (SHIP-742): signed deliveries through the real webhook endpoint, propose sessions started by
// the real round worker, the agent's answer (a proposal golden) through the real agent report path, and every GitHub call
// through the real GitHubIssuesClient against an in-process fake GitHub REST API. The background loops are off in the Test host,
// so Settle drives the round, proposal and accept workers and the outbox deliverer until nothing is due, and the progress
// reconciler is run by hand. The recorded payloads name acme/storefront, so each test maps it to a project of its own.
[TestFixture]
public partial class HappyJourneyTests : WebhookApiTest
{
    private const string Instance = "https://shiplean.example.test";
    private const string SelfLogin = "shiplean-bot";
    private const string Repo = "/repos/acme/storefront";

    // GitHub comment ids only grow: the fake's ids and the delivered comments' ids come from one counter.
    private static long _ids = 4_000_000_000;
    private static int _numbers = 7760;

    private readonly FakeGitHub _github = new();
    private readonly SwitchableSettings _settings = new();

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GithubKey] = GithubSecret };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IIssueIntakeSettings>(_settings);
        services.AddSingleton<IHttpClientFactory>(_github);
        services.AddSingleton<IIntegrationSettings>(new FixedIntegrationSettings(new IntegrationSettingsDto("ghp_test_token", "https://api.github.test/", null, null, [])));
    };

    private DateTimeOffset Now => Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();

    [SetUp]
    public async Task BaseUrlAsync()
    {
        _github.Reset();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE instance_settings SET base_url = @Instance", new { Instance });
    }

    [Test]
    public async Task GitHubIssue_WhenAcceptedAndShipped_ShouldConverseAndClose()
    {
        var journey = await ArrangeAsync();
        var number = Interlocked.Increment(ref _numbers);
        var issueRef = new IssueRef(IssueRef.GitHubHost, WebhookDelivery.GithubRepository, number);

        // Opened: a Triage Story and a first round.
        (await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "opened", number, title: "Filter by tag", body: "I want a tag filter."))).Outcome.ShouldBe(WebhookOutcome.Processed);
        var issue = await IssueAsync(issueRef);
        var triage = await ItemAsync(issue.WorkItemId);
        triage.ShouldSatisfyAllConditions(() => triage.Type.ShouldBe("story"), () => triage.IsIntake.ShouldBeTrue(), () => triage.Title.ShouldBe("Filter by tag"));
        await SettleAsync();
        var first = (await IssueAsync(issueRef)).ProposalSessionId.ShouldNotBeNull();

        // Round 1 asks questions.
        await RespondAsync(journey, first, Golden("needsInfo"));
        await SettleAsync();
        var questions = _github.Comments(number).ShouldHaveSingleItem();
        questions.ShouldContain("1. Which page were you on?");
        questions.ShouldContain(ProposalJobWorker.Footer);
        (await IssueAsync(issueRef)).Phase.ShouldBe(IssuePhase.AwaitingReply);

        // An edited comment is ignored, even an LGTM from a maintainer (doc check: edited comments are ignored).
        (await DeliverAsync(WebhookDelivery.GithubIssueComment(GithubSecret, "LGTM", number: number, commentId: NextId(), action: "edited"))).Outcome.ShouldBe(WebhookOutcome.Ignored);
        await SettleAsync();
        (await IssueAsync(issueRef)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await MirroredAsync(issue.Id)).ShouldBe(0);

        // The author answers: round 2 proposes a Story.
        (await DeliverAsync(WebhookDelivery.GithubIssueComment(
            GithubSecret, "The backlog page. /health shows 1.0.0+abc1234.", WebhookDelivery.IssueAuthor, "NONE", number, commentId: NextId()))).Outcome.ShouldBe(WebhookOutcome.Processed);
        await SettleAsync();
        var second = await IssueAsync(issueRef);
        second.Rounds.ShouldBe(2);
        var secondSession = second.ProposalSessionId.ShouldNotBeNull();
        secondSession.ShouldNotBe(first);
        await RespondAsync(journey, secondSession, Golden("new"));
        await SettleAsync();
        _github.Comments(number).Count.ShouldBe(2);
        _github.Comments(number)[1].ShouldStartWith("Thanks for the report.");

        // A maintainer's LGTM accepts it in place: the Triage item moves to New with the proposal's text.
        (await DeliverAsync(WebhookDelivery.GithubIssueComment(GithubSecret, "LGTM, thanks", number: number, commentId: NextId()))).Outcome.ShouldBe(WebhookOutcome.Processed);
        await SettleAsync();
        var accepted = await IssueAsync(issueRef);
        accepted.Phase.ShouldBe(IssuePhase.Accepted);
        accepted.WorkItemId.ShouldBe(triage.Id);
        var item = await ItemAsync(triage.Id);
        item.ShouldSatisfyAllConditions(
            () => item.Title.ShouldBe("Filter the backlog by tag"),
            () => item.State.ShouldBe("New"),
            () => item.IsIntake.ShouldBeFalse(),
            () => item.Criteria.ShouldContain("Tag = x shows only items tagged x"));
        _github.Comments(number)[2].ShouldBe(AcceptanceTexts.Accepted(item.Key, "Filter the backlog by tag"));

        // Planned for a sprint.
        var sprint = await AddSprintAsync(journey, "Sprint 9");
        await SetSprintAsync(item.Id, sprint);
        await ReconcileAsync(issue.Id);
        _github.Comments(number)[3].ShouldStartWith($"{item.Key} \"Filter the backlog by tag\" is planned for the sprint Sprint 9 (");

        // A pull request opens.
        var pull = Interlocked.Increment(ref _numbers);
        var opened = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", pull, $"{item.Key}: Filter the backlog by tag", Next()));
        opened.Linked.ShouldBe([item.Key]);
        await ReconcileAsync(issue.Id);
        _github.Comments(number)[4].ShouldBe(ProgressMessages.PullRequest(item.Key, "Filter the backlog by tag", $"https://github.com/acme/storefront/pull/{pull}", merged: false));

        // It merges with a sha, which closes the item; the image is published. The done message replaces a merged post.
        var sha = NewSha();
        var published = PublishRun(sha, "completed", "success");
        var merged = await DeliverAsync(WebhookDelivery.Github(
            GithubSecret, "closed", pull, $"{item.Key}: Filter the backlog by tag", Next(), state: "closed", merged: true, mergeCommitSha: sha));
        merged.Closed.ShouldBe([item.Key]);
        await ReconcileAsync(issue.Id);

        var posts = _github.Comments(number);
        posts.Count.ShouldBe(6);
        var done = posts[5];
        done.ShouldStartWith($"{item.Key} \"Filter the backlog by tag\" is done.\n\n{ShippedInSection.Heading}\n\n- {item.Key}: commit `{sha[..7]}` (acme/storefront#{pull}), merged ");
        done.ShouldContain($"Image published {Utc(published)} (run: {RunUrl(sha)}).");
        done.ShouldEndWith(ProgressMessages.ClosingCompleted);
        _github.Closes(number).ShouldBe(["closed completed"]);

        // The phase moves to closed on the pass after the close was sent.
        await ReconcileAsync(issue.Id);
        (await IssueAsync(issueRef)).Phase.ShouldBe(IssuePhase.Closed);
        _github.Comments(number).Count.ShouldBe(6);
    }

    [Test]
    public async Task GitHubIssue_WhenAlreadyShipped_ShouldCloseAfterLgtm()
    {
        var journey = await ArrangeAsync();

        // Shipped earlier: an item whose merged pull request's image is still being published.
        var (shippedId, shippedKey, _) = await journey.CreateItemAsync(Api);
        var sha = NewSha();
        PublishRun(sha, "in_progress", null);
        var pull = Interlocked.Increment(ref _numbers);
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", pull, $"{shippedKey}: Filter by Epic", Next(), state: "closed", merged: true, mergeCommitSha: sha));
        (await ItemAsync(shippedId)).Category.ShouldBe("completed");

        var number = Interlocked.Increment(ref _numbers);
        var issueRef = new IssueRef(IssueRef.GitHubHost, WebhookDelivery.GithubRepository, number);
        await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "opened", number));
        await SettleAsync();
        var issue = await IssueAsync(issueRef);

        // The agent names the item (a lead) and claims its own commit, publish time and run; only verified facts are posted, and the
        // reply posts now, with the image not yet published (doc check: the alreadyShipped reply does not wait for a publication).
        await RespondAsync(journey, issue.ProposalSessionId.ShouldNotBeNull(), Golden("alreadyShipped").Replace("SHIP-412", shippedKey, StringComparison.Ordinal));
        await SettleAsync();
        var reply = _github.Comments(number).ShouldHaveSingleItem();
        reply.ShouldStartWith("This already shipped.");
        reply.ShouldContain($"{ShippedInSection.Heading}\n\n- {shippedKey}: commit `{sha[..7]}` (acme/storefront#{pull}), merged ");
        reply.ShouldContain(ShippedInSection.NotYetPublished);
        reply.ShouldContain("It works only when:\n- Search indexing turned on");
        reply.ShouldNotContain("123456789");
        (await IssueAsync(issueRef)).Phase.ShouldBe(IssuePhase.AlreadyShipped);

        // The author's LGTM closes the issue as completed and removes the Triage item.
        (await DeliverAsync(WebhookDelivery.GithubIssueComment(GithubSecret, "lgtm", WebhookDelivery.IssueAuthor, "NONE", number, commentId: NextId()))).Outcome.ShouldBe(WebhookOutcome.Processed);
        await SettleAsync();

        _github.Comments(number).Count.ShouldBe(2);
        _github.Comments(number)[1].ShouldBe(AcceptanceTexts.Shipped);
        _github.Closes(number).ShouldBe(["closed completed"]);
        (await IssueAsync(issueRef)).Phase.ShouldBe(IssuePhase.Closed);
        (await ItemAsync(issue.WorkItemId)).Category.ShouldBe("removed");
        (await CommentsAsync(issue.WorkItemId)).ShouldContain(c => c.AgentName == "ShipLean" && c.Body == AcceptanceTexts.AlreadyShippedNote);
    }

    [Test]
    public async Task GitHubIssue_WhenTypeDiffers_ShouldReplaceTriageItem()
    {
        var journey = await ArrangeAsync();
        var number = Interlocked.Increment(ref _numbers);
        var issueRef = new IssueRef(IssueRef.GitHubHost, WebhookDelivery.GithubRepository, number);

        // A bug label makes a Triage Bug; the proposal is a Story.
        await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "opened", number, title: "Tags can't be filtered", labels: ["bug"]));
        var issue = await IssueAsync(issueRef);
        (await ItemAsync(issue.WorkItemId)).Type.ShouldBe("bug");
        await SettleAsync();
        await RespondAsync(journey, (await IssueAsync(issueRef)).ProposalSessionId.ShouldNotBeNull(), Golden("new"));
        await SettleAsync();

        (await DeliverAsync(WebhookDelivery.GithubIssueComment(GithubSecret, "LGTM", WebhookDelivery.IssueAuthor, "NONE", number, commentId: NextId()))).Outcome.ShouldBe(WebhookOutcome.Processed);
        await SettleAsync();

        var accepted = await IssueAsync(issueRef);
        accepted.Phase.ShouldBe(IssuePhase.Accepted);
        accepted.WorkItemId.ShouldNotBe(issue.WorkItemId);
        var made = await ItemAsync(accepted.WorkItemId);
        made.ShouldSatisfyAllConditions(() => made.Type.ShouldBe("story"), () => made.Title.ShouldBe("Filter the backlog by tag"), () => made.State.ShouldBe("New"));
        var triage = await ItemAsync(issue.WorkItemId);
        triage.Category.ShouldBe("removed");
        (await CommentsAsync(triage.Id)).ShouldContain(c => c.AgentName == "ShipLean" && c.Body == AcceptanceTexts.Became(made.Key));
        (await CommentsAsync(made.Id)).ShouldContain(c => c.Body == AcceptanceTexts.CameFrom(triage.Key));
        _github.Comments(number)[^1].ShouldBe(AcceptanceTexts.Accepted(made.Key, "Filter the backlog by tag"));

        // Progress follows the new item.
        await MoveAsync(journey, made.Id, "story", "Active");
        await ReconcileAsync(issue.Id);
        _github.Comments(number)[^1].ShouldBe(ProgressMessages.Started(made.Key, "Filter the backlog by tag"));
    }

    private static long NextId() => Interlocked.Increment(ref _ids);

    private static string NewSha() => Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()) + "00c0ffee";

    private static string Golden(string name)
        => File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "IssueIntake", "Proposals", name + ".md"));

    private static string GitHubTime(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Utc(DateTimeOffset at) => at.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string RunUrl(string sha) => $"https://github.com/acme/storefront/actions/runs/{sha[..6]}";

    // The commit (merged an hour ago) and one docker-publish.yml run on it that started 5 minutes later and, when completed,
    // finished 11 minutes after the merge. Returns that finish time, in whole seconds as GitHub writes it.
    private DateTimeOffset PublishRun(string sha, string status, string? conclusion)
    {
        var mergedAt = DateTimeOffset.FromUnixTimeSeconds(Now.AddHours(-1).ToUnixTimeSeconds());
        var finished = mergedAt.AddMinutes(11);
        _github.Respond($"{Repo}/commits/{sha}", $$"""{ "sha": "{{sha}}", "commit": { "committer": { "date": "{{GitHubTime(mergedAt)}}" } } }""");
        var runId = long.Parse(sha[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        _github.Respond($"{Repo}/actions/workflows/docker-publish.yml/runs", $$"""
            { "total_count": 1, "workflow_runs": [{ "id": {{runId}}, "head_sha": "{{sha}}", "status": "{{status}}",
              "conclusion": {{(conclusion is null ? "null" : $"\"{conclusion}\"")}}, "html_url": "{{RunUrl(sha)}}",
              "created_at": "{{GitHubTime(mergedAt.AddMinutes(5))}}", "updated_at": "{{GitHubTime(finished)}}" }] }
            """);
        return finished;
    }

    // A project of its own with a live relay connection, Triage states for Story, Bug and Feature, and acme/storefront taken by it
    // with Joel as the run-as member.
    private async Task<AgentDispatchArrange> ArrangeAsync()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        foreach (var type in new[] { "story", "bug", "feature" })
        {
            await database.ExecuteAsync(
                "INSERT INTO state (id, project_id, type, name, category, sort_order, is_intake) VALUES (@id, @project, @type, 'Triage', 'proposed', 0, true)",
                new { id = Guid.NewGuid(), project = arranged.Project.Id, type });
        }

        var key = await database.QuerySingleAsync<string>("SELECT key FROM project WHERE id = @id", new { id = arranged.Project.Id });
        _settings.Current = new FixedIssueIntakeSettings().Take(new IssueIntakeProject(arranged.Project.Id, key, WebhookDelivery.GithubRepository, SeedIds.Users.Joel, "Write a proposal.", 5));
        return arranged;
    }

    // The relay's answer to a propose session: one response activity, which also completes the session.
    private async Task RespondAsync(AgentDispatchArrange journey, Guid sessionId, string body)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAgentReports>().CreateActivityAsync(
            new AgentReporter(journey.ConnectionId!.Value, "Relay"), new AgentActivityCreate(sessionId, "response", body, null, null, null, false, null, null));
    }

    // Runs the round, proposal and accept workers and the outbox deliverer until none has anything due.
    private async Task SettleAsync()
    {
        var rounds = Host.Services.GetRequiredService<RoundJobWorker>();
        var proposals = Host.Services.GetRequiredService<ProposalJobWorker>();
        var accepts = Host.Services.GetRequiredService<AcceptJobWorker>();
        for (var pass = 0; pass < 20; pass++)
        {
            var worked = false;
            while (await rounds.RunNextAsync(CancellationToken.None) | await proposals.RunNextAsync(CancellationToken.None) | await accepts.RunNextAsync(CancellationToken.None))
            {
                worked = true;
            }

            if (await DeliverDueAsync() == 0 && !worked)
            {
                return;
            }
        }

        throw new ShouldAssertException("The workers never settled.");
    }

    // One deliverer pass with a clock a minute ahead of the host's, so every entry enqueued so far is due.
    private Task<int> DeliverDueAsync()
        => new GitHubOutboxDeliverer(
                Host.Services.GetRequiredService<NpgsqlDataSource>(), Host.Services.GetRequiredService<IServiceScopeFactory>(), Host.Services.GetRequiredService<IGitHubIdentity>(),
                new FakeTimeProvider(Now.AddMinutes(1)), NullLogger<GitHubOutboxDeliverer>.Instance)
            .RunDueAsync(CancellationToken.None);

    private async Task ReconcileAsync(Guid issueId)
    {
        await Host.Services.GetRequiredService<ProgressReconciler>().ReconcileAsync(issueId, CancellationToken.None);
        await SettleAsync();
    }

    private async Task<GitHubIssueRecord> IssueAsync(IssueRef issue)
        => (await Host.Services.GetRequiredService<IGitHubIssueStore>().FindAsync(issue)).ShouldNotBeNull();

    private async Task<ItemRow> ItemAsync(Guid id)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<ItemRow>(
            """
            SELECT w.id, w.key, w.type, w.title, s.name AS state, s.category, s.is_intake AS isIntake, w.acceptance_criteria::text AS criteria
            FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.id = @id
            """,
            new { id });
    }

    private async Task<List<CommentRow>> CommentsAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<CommentRow>(
            "SELECT agent_name AS agentName, body FROM comment WHERE work_item_id = @workItemId ORDER BY created_at", new { workItemId })];
    }

    private async Task<int> MirroredAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<int>("SELECT count(*) FROM github_issue_comment WHERE github_issue_id = @issueId", new { issueId });
    }

    private async Task<Guid> AddSprintAsync(AgentDispatchArrange journey, string name)
    {
        var id = Guid.NewGuid();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @projectId, @name, current_date, current_date + 13)",
            new { id, projectId = journey.Project.Id, name });
        return id;
    }

    private async Task SetSprintAsync(Guid itemId, Guid sprintId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE work_item SET sprint_id = @sprintId WHERE id = @itemId", new { itemId, sprintId });
    }

    private async Task MoveAsync(AgentDispatchArrange journey, Guid itemId, string type, string state)
    {
        await using var database = await OpenDatabaseAsync();
        var revision = await database.QuerySingleAsync<int>("SELECT revision FROM work_item WHERE id = @itemId", new { itemId });
        var stateId = await database.QuerySingleAsync<Guid>(
            "SELECT id FROM state WHERE project_id = @projectId AND type = @type AND name = @state", new { projectId = journey.Project.Id, type, state });
        await AgentDispatchArrange.MoveAsync(Api, itemId, revision, stateId);
    }

    [GeneratedRegex(@"\n\n<!-- shiplean:outbox:[0-9a-f-]{36} -->$")]
    private static partial Regex Marker();

    // The fixture's one IIssueIntakeSettings; each test points acme/storefront at its own project.
    private sealed class SwitchableSettings : IIssueIntakeSettings
    {
        public FixedIssueIntakeSettings Current { get; set; } = new();

        public Task<IssueIntakeProject?> ForRepositoryAsync(IssueRef issue, CancellationToken cancellationToken = default) => Current.ForRepositoryAsync(issue, cancellationToken);
    }

    private sealed record ItemRow(Guid Id, string Key, string Type, string Title, string State, string Category, bool IsIntake, string Criteria);

    private sealed record CommentRow(string? AgentName, string Body);

    // An IHttpClientFactory whose clients play GitHub's REST API for acme/storefront: comments get growing ids, PATCH on an issue
    // is a state change, GET /user is the token's login, comment lists are empty, and Respond sets other GET answers by path
    // (404 for any other). The loopback HttpsStubServer can't serve these paths (it skips paths whose last segment has a dot).
    private sealed class FakeGitHub : IHttpClientFactory
    {
        private readonly ConcurrentQueue<(int Number, string Kind, string Body)> _writes = new();
        private readonly ConcurrentDictionary<string, string> _answers = new(StringComparer.Ordinal);

        public void Reset()
        {
            _writes.Clear();
            _answers.Clear();
        }

        public void Respond(string path, string body) => _answers[path] = body;

        // The comment bodies posted on the issue, in order.
        public IReadOnlyList<string> Comments(int number) => [.. _writes.Where(w => w.Number == number && w.Kind == "comment").Select(w => w.Body)];

        // The state changes sent for the issue, as "closed completed".
        public IReadOnlyList<string> Closes(int number) => [.. _writes.Where(w => w.Number == number && w.Kind == "state").Select(w => w.Body)];

        public HttpClient CreateClient(string name) => new(new Answerer(this));

        private sealed class Answerer(FakeGitHub owner) : HttpMessageHandler
        {
            private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri!.AbsolutePath;
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var isIssue = path.StartsWith($"{Repo}/issues/", StringComparison.Ordinal);
                var number = isIssue ? int.Parse(segments[4], CultureInfo.InvariantCulture) : 0;
                if (request.Method == HttpMethod.Post && isIssue && segments.Length == 6 && segments[5] == "comments")
                {
                    var body = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.GetProperty("body").GetString()!;
                    // The deliverer adds the outbox marker at send time; tests compare the text before it.
                    var marker = Marker().Match(body);
                    marker.Success.ShouldBeTrue("a posted comment carries the outbox marker");
                    owner._writes.Enqueue((number, "comment", body[..marker.Index]));
                    return Json(HttpStatusCode.Created, $$"""{ "id": {{NextId()}} }""");
                }

                if (request.Method == HttpMethod.Patch && isIssue && segments.Length == 5)
                {
                    var state = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement;
                    owner._writes.Enqueue((number, "state", $"{state.GetProperty("state").GetString()} {state.GetProperty("state_reason").GetString()}"));
                    return Json(HttpStatusCode.OK, "{}");
                }

                if (request.Method == HttpMethod.Get && isIssue && segments.Length == 6 && segments[5] == "comments")
                {
                    return Json(HttpStatusCode.OK, "[]");
                }

                if (request.Method == HttpMethod.Get && path == "/user")
                {
                    return Json(HttpStatusCode.OK, $$"""{ "login": "{{SelfLogin}}" }""");
                }

                return request.Method == HttpMethod.Get && owner._answers.TryGetValue(path, out var answer)
                    ? Json(HttpStatusCode.OK, answer)
                    : Json(HttpStatusCode.NotFound, """{ "message": "Not Found" }""");
            }
        }
    }
}
