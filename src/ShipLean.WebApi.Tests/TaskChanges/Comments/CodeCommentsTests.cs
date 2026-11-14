using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Mcp;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.WebApi.Tests.TaskChanges.Comments;

// ICodeComments and its three routes on the real host and database. Commits are seeded with CodeChangesSeed and patches come from FakeCodeHost.
[TestFixture]
public class CodeCommentsTests : ApiFixture
{
    private static readonly RepoRef Store = FakeCodeHost.StorefrontRepo;

    private readonly SettableCurrentAgent _agent = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddFakeCodeHost();
        services.RemoveAll<ICurrentAgent>();
        services.AddSingleton<ICurrentAgent>(_agent);
    };

    [SetUp]
    public void NoAgentByDefault() => _agent.Clear();

    [Test]
    public async Task AddComment_WhenShaNotOnItem_ShouldRefuseWithFieldSha()
    {
        var (item, _) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/code-comments", Request(FakeCodeHost.Shas.Third));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("sha");
    }

    [Test]
    public async Task AddComment_WhenSameShaInOtherRepo_ShouldRefuse()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        await seed.CommitAsync(item, FakeCodeHost.Payments, FakeCodeHost.Shas.PaymentsOnly);

        // Payments has its own commit, but the comment names storefront with a sha that exists only in payments.
        using var response = await Api.PostJsonAsync(
            $"api/work-items/{item}/code-comments", Request(FakeCodeHost.Shas.PaymentsOnly));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("sha");
    }

    [Test]
    public async Task AddComment_WhenShaIsShortAndOnItem_ShouldStoreFullShaAndLabelTheTime()
    {
        var (item, _) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/code-comments", Request(FakeCodeHost.Shas.First[..8]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = await ReadAsync<CodeCommentDto>(response);
        dto.Sha.ShouldBe(FakeCodeHost.Shas.First);
        dto.Repo.ShouldBe(FakeCodeHost.Storefront);
        dto.Side.ShouldBe("new");
        dto.By.ShouldStartWith("Joel");
        dto.ByAgent.ShouldBeFalse();
        dto.CreatedAtLabel.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task AddComment_WhenBodyHasScript_ShouldStoreSanitisedHtml()
    {
        var (item, _) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);

        using var response = await Api.PostJsonAsync(
            $"api/work-items/{item}/code-comments", Request(FakeCodeHost.Shas.First) with { Body = "Hi <script>alert(1)</script> **bold**" });

        var dto = await ReadAsync<CodeCommentDto>(response);
        dto.BodyHtml.ShouldNotContain("<script");
        dto.BodyHtml.ShouldContain("<strong>bold</strong>");
    }

    [Test]
    public async Task AddComment_WhenAgentCalls_ShouldStampTheAgent()
    {
        var (item, _) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        _agent.ActAs("claude", null, true);

        var added = await InScopeAsync(c => c.AddAsync(item, New(FakeCodeHost.Shas.First), default));

        added.ByAgent.ShouldBeTrue();
        added.By.ShouldContain("claude");
    }

    [Test]
    public async Task Reply_WhenParentIsReply_ShouldRefuse()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var comment = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");
        var reply = await seed.ReplyAsync(item, comment, "Because");

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/code-comments/{reply}/replies", new ReplyCodeCommentRequest("Again"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Reply_WhenCommentOnOtherItem_ShouldReturnNotFound()
    {
        var (item, _) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var (other, otherSeed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var comment = await otherSeed.CommentAsync(other, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/code-comments/{comment}/replies", new ReplyCodeCommentRequest("Hi"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Resolve_WhenResolvedThenReopened_ShouldSetAndClearResolvedAt()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var comment = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");
        var route = $"api/work-items/{item}/code-comments/{comment}/resolve";

        (await Api.PostJsonAsync(route, new ResolveCodeCommentRequest(true))).IsSuccessStatusCode.ShouldBeTrue();
        (await InScopeAsync(c => c.OpenAsync(item, default))).ShouldBeEmpty();
        (await Api.PostJsonAsync(route, new ResolveCodeCommentRequest(false))).IsSuccessStatusCode.ShouldBeTrue();

        (await InScopeAsync(c => c.OpenAsync(item, default))).Select(c => c.Id).ShouldBe([comment]);
    }

    [Test]
    public async Task List_WhenRepliesExist_ShouldNestThemUnderTheirComment()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var comment = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");
        await seed.ReplyAsync(item, comment, "Because");

        var listed = await InScopeAsync(c => c.ListAsync(item, default));

        var top = listed.ShouldHaveSingleItem();
        top.Replies.Select(r => r.Body).ShouldBe(["Because"]);
        top.Repo.ShouldBe(FakeCodeHost.Storefront);
    }

    [Test]
    public async Task Answer_WhenIdsOnItem_ShouldReplyAndResolveEach()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var one = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "One?");
        var two = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 2, "Two?");
        _agent.ActAs("claude", null, true);

        await InScopeAsync(c => c.AnswerAsync(item, [new CodeCommentAnswer(one, "Fixed one"), new CodeCommentAnswer(two, "Fixed two")], default));

        var listed = await InScopeAsync(c => c.ListAsync(item, default));
        listed.ShouldAllBe(c => c.ResolvedAt != null);
        listed.Select(c => c.Replies.Single().Body).ShouldBe(["Fixed one", "Fixed two"]);
        listed[0].Replies[0].ByAgent.ShouldBeTrue();
    }

    [Test]
    public async Task Answer_WhenOneIdFromAnotherItem_ShouldWriteNothing()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var (other, otherSeed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var mine = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Mine?");
        var theirs = await otherSeed.CommentAsync(other, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Theirs?");

        var refused = await Should.ThrowAsync<ApiException>(
            () => InScopeAsync(c => c.AnswerAsync(item, [new CodeCommentAnswer(mine, "Done"), new CodeCommentAnswer(theirs, "Done")], default)));

        refused.Field.ShouldBe("answered_comments");
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM code_comment WHERE work_item_id IN (@item, @other) AND (parent_id IS NOT NULL OR resolved_at IS NOT NULL)", new { item, other })).ShouldBe(0);
    }

    [Test]
    public async Task Answer_WhenSentTwice_ShouldNotDuplicateReply()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var comment = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "One?");
        _agent.ActAs("claude", null, true);

        await InScopeAsync(c => c.AnswerAsync(item, [new CodeCommentAnswer(comment, "Fixed")], default));
        await InScopeAsync(c => c.AnswerAsync(item, [new CodeCommentAnswer(comment, "Fixed")], default));

        (await InScopeAsync(c => c.ListAsync(item, default))).Single().Replies.Count.ShouldBe(1);
    }

    [Test]
    public async Task MarkOutdated_WhenLaterCommitTouchesLine_ShouldSetOutdated()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        var comment = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/checkout.ts", CodeSide.New, 2, "Why 1?");
        var second = await seed.CommitAsync(item, Store, FakeCodeHost.Shas.Second);

        await InScopeAsync(c => c.MarkOutdatedAsync(item, [Added(second, FakeCodeHost.Shas.Second)], default));

        (await InScopeAsync(c => c.ListAsync(item, default))).Single(c => c.Id == comment).OutdatedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task MarkOutdated_WhenHunkElsewhere_ShouldLeaveOpen()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.First);
        // Third adds basket.ts line 4 (hunk new lines 1-4); the comment is on line 5, just past it, and on a file Third does not touch.
        var past = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.Second, "src/basket.ts", CodeSide.New, 5, "Past?");
        var readme = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "README.md", CodeSide.New, 1, "Docs?");
        var third = await seed.CommitAsync(item, Store, FakeCodeHost.Shas.Third);

        await InScopeAsync(c => c.MarkOutdatedAsync(item, [Added(third, FakeCodeHost.Shas.Third)], default));

        (await InScopeAsync(c => c.OpenAsync(item, default))).Select(c => c.OutdatedAt).ShouldAllBe(o => o == null);
        past.ShouldNotBe(readme);
    }

    [Test]
    public async Task MarkOutdated_WhenLastLineOfHunk_ShouldSetOutdated()
    {
        var (item, seed) = await ItemWithCommitsAsync(FakeCodeHost.Shas.Second);
        var comment = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.Second, "src/basket.ts", CodeSide.New, 4, "Edge?");
        var third = await seed.CommitAsync(item, Store, FakeCodeHost.Shas.Third);

        await InScopeAsync(c => c.MarkOutdatedAsync(item, [Added(third, FakeCodeHost.Shas.Third)], default));

        (await InScopeAsync(c => c.ListAsync(item, default))).Single(c => c.Id == comment).OutdatedAt.ShouldNotBeNull();
    }

    private static NewCodeComment New(string sha) => new(FakeCodeHost.Storefront, sha, "src/cart.ts", CodeSide.New, 1, null, "export const price = 10;", "Why?");

    private static AddCodeCommentRequest Request(string sha) => new(FakeCodeHost.Storefront, sha, "src/cart.ts", "new", 1, null, "export const price = 10;", "Why?");

    private static TaskCommit Added(Guid id, string sha) => new(
        id, Store, sha, 2, "subject", "Ada", DateTimeOffset.UnixEpoch, 1, 0, [], [], CommitSource.AddCommits, "Joel", false, DateTimeOffset.UnixEpoch);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        => System.Text.Json.JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

    // A new task whose live commits are shas of the storefront repository.
    private async Task<(Guid Item, CodeChangesSeed Seed)> ItemWithCommitsAsync(params string[] shas)
    {
        var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        var seed = new CodeChangesSeed(connection);
        foreach (var sha in shas)
        {
            await seed.CommitAsync(item, Store, sha);
        }

        return (item, seed);
    }

    private async Task<T> InScopeAsync<T>(Func<ICodeComments, Task<T>> action)
    {
        Host.ActAs(SeedIds.Users.Joel);
        try
        {
            await using var scope = Host.Services.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<ICodeComments>());
        }
        finally
        {
            Host.ActAs(null);
        }
    }

    private Task InScopeAsync(Func<ICodeComments, Task> action) => InScopeAsync(async c =>
    {
        await action(c);
        return 0;
    });
}
