using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.WebApi.Tests.TaskChanges.Review;

// Approve and Request changes on the real host and database: head check, state moves, sent_back_at, history line, note comment.
[TestFixture]
public class CodeReviewTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    [Test]
    public async Task Approve_WhenHeadIsNewest_ShouldCloseAndRecordCommits()
    {
        var (item, commits, _) = await ArrangeAsync(FakeCodeHost.Shas.First, FakeCodeHost.Shas.Second);

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/approve", new ApproveChangesRequest(null, commits[^1]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await ReadAsync<ReviewOutcomeDto>(response)).ShouldBe(new ReviewOutcomeDto("Closed", 0));
        (await StateOfAsync(item)).ShouldBe("Closed");
        (await ReviewLinesAsync(item)).ShouldBe([$"Approved {FakeCodeHost.Shas.First[..7]}, {FakeCodeHost.Shas.Second[..7]}"]);
    }

    [Test]
    public async Task Approve_WhenNewerCommitExists_ShouldConflict()
    {
        var (item, commits, seed) = await ArrangeAsync(FakeCodeHost.Shas.First);
        await seed.CommitAsync(item, FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.Second);

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/approve", new ApproveChangesRequest(null, commits[0]));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        (await StateOfAsync(item)).ShouldBe("Active");
    }

    [Test]
    public async Task Approve_WhenSameShaInTwoRepos_ShouldUseCommitId()
    {
        var (item, commits, _) = await ArrangeAsync(FakeCodeHost.Shas.First);
        var newer = await SeedSameShaInPaymentsAsync(item, FakeCodeHost.Shas.First);

        using var older = await Api.PostJsonAsync($"api/work-items/{item}/changes/approve", new ApproveChangesRequest(null, commits[0]));
        (await ApiClient.ReadApiErrorAsync(older, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);

        using var head = await Api.PostJsonAsync($"api/work-items/{item}/changes/approve", new ApproveChangesRequest(null, newer));
        head.StatusCode.ShouldBe(HttpStatusCode.OK, await head.Content.ReadAsStringAsync());
        (await StateOfAsync(item)).ShouldBe("Closed");
    }

    [Test]
    public async Task Approve_WhenOpenComments_ShouldKeepThemOpen()
    {
        var (item, commits, seed) = await ArrangeAsync(FakeCodeHost.Shas.First);
        var comment = await seed.CommentAsync(item, FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/approve", new ApproveChangesRequest(null, commits[0]));

        (await ReadAsync<ReviewOutcomeDto>(response)).ShouldBe(new ReviewOutcomeDto("Closed", 1));
        await using var database = await OpenDatabaseAsync();
        var row = await database.QuerySingleAsync<(DateTimeOffset? Resolved, DateTimeOffset? SentBack)>(
            "SELECT resolved_at, sent_back_at FROM code_comment WHERE id = @comment", new { comment });
        row.Resolved.ShouldBeNull();
        row.SentBack.ShouldBeNull();
    }

    [Test]
    public async Task Approve_WhenNote_ShouldPostComment()
    {
        var (item, commits, _) = await ArrangeAsync(FakeCodeHost.Shas.First);

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/approve", new ApproveChangesRequest("  Ship it.  ", commits[0]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await CommentBodiesAsync(item)).ShouldBe(["Ship it."]);
    }

    [Test]
    public async Task Approve_WhenNoCommits_ShouldConflict()
    {
        var (item, _, _) = await ArrangeAsync();

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/approve", new ApproveChangesRequest(null, Guid.NewGuid()));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task RequestChanges_WhenDefault_ShouldMoveToFirstProposedAndUnassign()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.First);

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/request", new RequestTaskChangesRequest("Please redo.", false));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await ReadAsync<ReviewOutcomeDto>(response)).ShouldBe(new ReviewOutcomeDto("New", 0));
        (await StateOfAsync(item)).ShouldBe("New");
        (await AssigneeOfAsync(item)).ShouldBeNull();
        (await ReviewLinesAsync(item)).ShouldBe(["Changes requested · 0 comments"]);
        (await CommentBodiesAsync(item)).ShouldBe(["Please redo."]);
    }

    [Test]
    public async Task RequestChanges_WhenKeepAssignee_ShouldMoveToActiveAndKeepAssignee()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.First);
        await SetStateAsync(item, "Closed");

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/request", new RequestTaskChangesRequest(null, true));

        (await ReadAsync<ReviewOutcomeDto>(response)).ShouldBe(new ReviewOutcomeDto("Active", 0));
        (await StateOfAsync(item)).ShouldBe("Active");
        (await AssigneeOfAsync(item)).ShouldBe(SeedIds.Users.Priya);
    }

    [Test]
    public async Task RequestChanges_WhenOpenComments_ShouldMarkOnlyOpenTopLevelSentBack()
    {
        var (item, _, seed) = await ArrangeAsync(FakeCodeHost.Shas.First);
        var repo = FakeCodeHost.StorefrontRepo;
        var open = await seed.CommentAsync(item, repo, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Open one");
        var reply = await seed.ReplyAsync(item, open, "A reply");
        var resolved = await seed.CommentAsync(item, repo, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 2, "Done one");
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE code_comment SET resolved_at = now() WHERE id = @resolved", new { resolved });

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/request", new RequestTaskChangesRequest(null, false));

        (await ReadAsync<ReviewOutcomeDto>(response)).ShouldBe(new ReviewOutcomeDto("New", 1));
        var marked = (await database.QueryAsync<Guid>("SELECT id FROM code_comment WHERE work_item_id = @item AND sent_back_at IS NOT NULL", new { item })).ToList();
        marked.ShouldBe([open]);
        marked.ShouldNotContain(reply);
        (await ReviewLinesAsync(item)).ShouldBe(["Changes requested · 1 comment"]);
    }

    [Test]
    public async Task RequestChanges_WhenCustomProposedStateFirst_ShouldUseIt()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.First);
        await using var database = await OpenDatabaseAsync();
        var triage = Guid.NewGuid();
        await database.ExecuteAsync(
            "INSERT INTO state (id, project_id, type, name, category, sort_order) VALUES (@triage, @project, 'task', 'Triage', 'proposed', -10)",
            new { triage, project = SeedIds.Projects.Ship });
        try
        {
            using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/request", new RequestTaskChangesRequest(null, false));

            (await ReadAsync<ReviewOutcomeDto>(response)).ShouldBe(new ReviewOutcomeDto("Triage", 0));
            (await StateOfAsync(item)).ShouldBe("Triage");
        }
        finally
        {
            await database.ExecuteAsync("UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @project AND type = 'task' AND name = 'New' AND archived_at IS NULL) WHERE id = @item", new { item, project = SeedIds.Projects.Ship });
            await database.ExecuteAsync("UPDATE state SET archived_at = now() WHERE id = @triage", new { triage });
        }
    }

    [Test]
    public async Task Approve_WhenNotMember_ShouldReturnNotFound()
    {
        await using var database = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Part).WithType("task").InsertAsync(database);

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/changes/approve", new ApproveChangesRequest(null, Guid.NewGuid()));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    // A task in Active, assigned to Priya, with the given commits in storefront, oldest first.
    private async Task<(Guid Item, List<Guid> Commits, CodeChangesSeed Seed)> ArrangeAsync(params string[] shas)
    {
        var database = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithStateNamed("Active").WithAssignee(SeedIds.Users.Priya).InsertAsync(database);
        var seed = new CodeChangesSeed(database);
        var commits = new List<Guid>();
        foreach (var sha in shas)
        {
            commits.Add(await seed.CommitAsync(item, FakeCodeHost.StorefrontRepo, sha));
        }

        return (item, commits, seed);
    }

    // The same sha as a newer commit of the payments repository: the fake has no such commit, so the rows are copied.
    private async Task<Guid> SeedSameShaInPaymentsAsync(Guid item, string sha)
    {
        await using var database = await OpenDatabaseAsync();
        var seed = new CodeChangesSeed(database);
        var repository = await seed.RepositoryAsync(FakeCodeHost.Payments);
        var id = Guid.NewGuid();
        await database.ExecuteAsync(
            """
            INSERT INTO code_commit (repository_id, sha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at)
            SELECT @repository, sha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at FROM code_commit WHERE sha = @sha LIMIT 1;
            INSERT INTO work_item_commit (id, work_item_id, repository_id, sha, position, added_via)
            VALUES (@id, @item, @repository, @sha, 99, 'add_commits');
            """,
            new { repository, sha, id, item });
        return id;
    }

    private async Task<string> StateOfAsync(Guid item)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<string>("SELECT s.name FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.id = @item", new { item });
    }

    private async Task SetStateAsync(Guid item, string name)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @project AND type = 'task' AND name = @name AND archived_at IS NULL) WHERE id = @item",
            new { item, name, project = SeedIds.Projects.Ship });
    }

    private async Task<Guid?> AssigneeOfAsync(Guid item)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<Guid?>("SELECT assigned_to FROM work_item WHERE id = @item", new { item });
    }

    private async Task<List<string>> CommentBodiesAsync(Guid item)
    {
        await using var database = await OpenDatabaseAsync();
        return (await database.QueryAsync<string>("SELECT body FROM comment WHERE work_item_id = @item AND archived_at IS NULL ORDER BY created_at", new { item })).ToList();
    }

    private async Task<List<string?>> ReviewLinesAsync(Guid item)
    {
        using var response = await Api.GetAsync($"api/work-items/{item}/history");
        var history = JsonSerializer.Deserialize<HistoryDto>(await response.Content.ReadAsStringAsync(), Wire)!;
        return [.. history.Revisions.SelectMany(r => r.Changes).Where(c => c.Field == "review").Select(c => c.ToLabel)];
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        => JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), Wire)!;
}
