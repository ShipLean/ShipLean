using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport.TaskChanges;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Reports;

// report_task's commits with a repo, over the seeded plan with the fake code host (acme/storefront, acme/payments, and acme/secret that it can't read).
[TestFixture]
public class ReportTaskCommitsTests : ReportTestBase
{
    private const string Store = "github.com/acme/storefront";
    private const string Payments = "github.com/acme/payments";
    private const string Secret = "github.com/acme/secret";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddFakeCodeHost();
    };

    [OneTimeSetUp]
    public Task Seed() => SeedPlanAsync();

    [Test]
    public async Task ReportTask_WhenCommitHasRepo_ShouldStoreWorkItemCommitNotCard()
    {
        var task = NextTask();

        var reply = await ReportAsync($$"""{"item":"{{task}}","status":"done","commits":[{"repo":"{{Store}}","sha":"{{FakeCodeHost.Shas.First}}","subject":"Price"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var stored = await CommitsOfAsync(task);
        stored.Select(c => (c.Repo.Name, c.Sha)).ShouldBe([(FakeCodeHost.Storefront, FakeCodeHost.Shas.First)]);
        (await LinksOfAsync(task)).ShouldBeEmpty();
        reply.Structured().GetProperty("commits_added").GetInt32().ShouldBe(1);
        reply.Structured().GetProperty("commits_not_added").GetArrayLength().ShouldBe(0);
        (await CommentsOfAsync(task)).Single().Body.ShouldContain($"`{FakeCodeHost.Shas.First[..7]}` Price (https://github.com/acme/storefront/commit/{FakeCodeHost.Shas.First})");
    }

    [Test]
    public async Task ReportTask_WhenOldShortShaAndUrl_ShouldResolveRepoFromUrl()
    {
        var task = NextTask();
        var url = $"https://github.com/acme/storefront/commit/{FakeCodeHost.Shas.First}";

        var reply = await ReportAsync($$"""{"item":"{{task}}","status":"done","commits":[{"sha":"{{FakeCodeHost.Shas.First[..8]}}","url":"{{url}}"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        (await CommitsOfAsync(task)).Select(c => (c.Repo.Name, c.Sha)).ShouldBe([(FakeCodeHost.Storefront, FakeCodeHost.Shas.First)]);
        (await LinksOfAsync(task)).ShouldBeEmpty();
    }

    [Test]
    public async Task ReportTask_WhenOneRepoUnreadable_ShouldAddOthersAndListNotAdded()
    {
        var task = NextTask();

        var reply = await ReportAsync($$"""
            {"item":"{{task}}","status":"done","commits":[
              {"repo":"{{Store}}","sha":"{{FakeCodeHost.Shas.First}}"},
              {"repo":"{{Secret}}","sha":"{{FakeCodeHost.Shas.First}}","subject":"Hidden"}]}
            """);

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        (await CommitsOfAsync(task)).Select(c => c.Repo.Name).ShouldBe([FakeCodeHost.Storefront]);
        var links = await LinksOfAsync(task);
        links.Select(l => l.Url).ShouldBe([$"https://github.com/acme/secret/commit/{FakeCodeHost.Shas.First}"]);
        NotAdded(reply).ShouldBe([(FakeCodeHost.Shas.First, FakeCodeHost.Secret, "repo_unreadable")]);
        (await StateOfAsync(task)).ShouldBe("Closed");
    }

    [Test]
    public async Task ReportTask_WhenOneCommitMissing_ShouldListItAndKeepTheRest()
    {
        var task = NextTask();

        var reply = await ReportAsync($$"""
            {"item":"{{task}}","status":"done","commits":[
              {"repo":"{{Store}}","sha":"{{FakeCodeHost.Shas.First}}"},
              {"repo":"{{Payments}}","sha":"{{FakeCodeHost.Shas.Missing}}"}]}
            """);

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        (await CommitsOfAsync(task)).Select(c => c.Repo.Name).ShouldBe([FakeCodeHost.Storefront]);
        NotAdded(reply).ShouldBe([(FakeCodeHost.Shas.Missing, FakeCodeHost.PaymentsRepo, "bad_sha")]);
        (await LinksOfAsync(task)).Select(l => l.Url).ShouldBe([$"https://github.com/acme/payments/commit/{FakeCodeHost.Shas.Missing}"]);
    }

    [Test]
    public async Task ReportTask_WhenRepoHostUnknown_ShouldListNotAddedWithoutCard()
    {
        var task = NextTask();

        var reply = await ReportAsync($$"""{"item":"{{task}}","status":"done","commits":[{"repo":"git.example.test/acme/ship","sha":"abc1234def"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        NotAdded(reply).ShouldBe([("abc1234def", "git.example.test/acme/ship", "repo_unreadable")]);
        (await LinksOfAsync(task)).ShouldBeEmpty();
        (await CommitsOfAsync(task)).ShouldBeEmpty();
    }

    [Test]
    public async Task ReportTask_WhenResentAfterSuccess_ShouldNotDuplicateRepliesOrCommits()
    {
        var task = NextTask();
        await using var connection = await OpenDatabaseAsync();
        var item = await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @task", new { task });
        var first = $$"""{"item":"{{task}}","status":"partial","commits":[{"repo":"{{Store}}","sha":"{{FakeCodeHost.Shas.First}}"}]}""";
        (await ReportAsync(first)).IsToolError().ShouldBeFalse();
        var comment = await new CodeChangesSeed(connection).CommentAsync(item, FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");
        var again = $$"""{"item":"{{task}}","status":"partial","commits":[{"repo":"{{Store}}","sha":"{{FakeCodeHost.Shas.First}}"}],"answered_comments":[{"id":"{{comment}}","reply":"Fixed."}]}""";

        (await ReportAsync(again)).IsToolError().ShouldBeFalse();
        (await ReportAsync(again)).IsToolError().ShouldBeFalse();

        (await CommitsOfAsync(task)).Count.ShouldBe(1);
        await using var scope = Host.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<ICodeComments>().ListAsync(item, default)).Single().Replies.Count.ShouldBe(1);
    }

    private static IReadOnlyList<(string Sha, string Repo, string Reason)> NotAdded(McpReply reply)
        => [.. reply.Structured().GetProperty("commits_not_added").EnumerateArray().Select(e => (e.GetProperty("sha").GetString()!, e.GetProperty("repo").GetString()!, e.GetProperty("reason").GetString()!))];

    private async Task<IReadOnlyList<TaskCommit>> CommitsOfAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @key", new { key });
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITaskCommits>().ListAsync(item, default);
    }
}
