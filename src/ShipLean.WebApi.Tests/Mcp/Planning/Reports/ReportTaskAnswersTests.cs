using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Reports;

// report_task's answered_comments, over the seeded plan with the fake code host.
[TestFixture]
public class ReportTaskAnswersTests : ReportTestBase
{
    private static readonly RepoRef Store = FakeCodeHost.StorefrontRepo;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddFakeCodeHost();
    };

    [OneTimeSetUp]
    public Task Seed() => SeedPlanAsync();

    [Test]
    public async Task ReportTask_WhenAnsweredComments_ShouldReplyAndResolve()
    {
        var task = NextTask();
        var (item, comment) = await TaskWithCommentAsync(task);

        var reply = await ReportAsync($$"""{"item":"{{task}}","status":"partial","answered_comments":[{"id":"{{comment}}","reply":"Fixed in the next commit."}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("answered_comments").GetInt32().ShouldBe(1);
        var listed = await ListCommentsAsync(item);
        listed.Single().ResolvedAt.ShouldNotBeNull();
        listed.Single().Replies.Select(r => r.Body).ShouldBe(["Fixed in the next commit."]);
        listed.Single().Replies.Single().ByAgent.ShouldBeTrue();
    }

    [Test]
    public async Task ReportTask_WhenAnsweredCommentNotOnTask_ShouldRefuseAndWriteNothing()
    {
        var task = NextTask();
        var (item, mine) = await TaskWithCommentAsync(task);
        var (_, theirs) = await TaskWithCommentAsync(NextTask());

        var reply = await ReportAsync($$"""{"item":"{{task}}","status":"done","answered_comments":[{"id":"{{mine}}","reply":"Done"},{"id":"{{theirs}}","reply":"Done"}]}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        (error.GetProperty("code").GetString(), error.GetProperty("field").GetString()).ShouldBe((ApiErrorCodes.NotFound, "answered_comments"));
        (await ListCommentsAsync(item)).Single().ResolvedAt.ShouldBeNull();
        (await CommentsOfAsync(task)).ShouldBeEmpty();
        (await StateOfAsync(task)).ShouldBe("New");
    }

    [Test]
    public async Task ReportTask_WhenAnswerHasNoReply_ShouldBeValidationBeforeAnyWrite()
    {
        var task = NextTask();
        var (item, comment) = await TaskWithCommentAsync(task);

        var reply = await ReportAsync($$"""{"item":"{{task}}","status":"partial","answered_comments":[{"id":"{{comment}}","reply":" "}]}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("answered_comments[0].reply");
        (await ListCommentsAsync(item)).Single().ResolvedAt.ShouldBeNull();
    }

    private async Task<(Guid Item, Guid Comment)> TaskWithCommentAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @key", new { key });
        var seed = new CodeChangesSeed(connection);
        await seed.CommitAsync(item, Store, FakeCodeHost.Shas.First);
        var comment = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");
        return (item, comment);
    }

    private async Task<IReadOnlyList<CodeComment>> ListCommentsAsync(Guid item)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICodeComments>().ListAsync(item, default);
    }
}
