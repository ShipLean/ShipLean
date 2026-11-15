using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TaskChanges;
using ShipLean.WebApi.Tests.Mcp.Transport;
using static ShipLean.TestSupport.TaskChanges.FakeCodeHost;

namespace ShipLean.WebApi.Tests.Mcp.Code;

// add_commits in SHIP, as Joel with a read-write token, against the fake code host.
[TestFixture]
public class AddCommitsToolTests : McpFixture
{
    private const string StorefrontHost = "github.com/acme/storefront";

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddFakeCodeHost();
    };

    [Test]
    public async Task AddCommitsTool_WhenTask_ShouldAddAndReturnSubjects()
    {
        var key = await NewItemKeyAsync("task");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "add_commits", Args(key, Commit(StorefrontHost, Shas.First)));

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        var added = result.GetProperty("added").EnumerateArray().ToList();
        added.ShouldHaveSingleItem();
        added[0].GetProperty("repo").GetString().ShouldBe(Storefront);
        added[0].GetProperty("sha").GetString().ShouldBe(Shas.First);
        added[0].GetProperty("subject").GetString().ShouldBe(MetaOf(Storefront, Shas.First).Subject);
        result.GetProperty("skipped").GetArrayLength().ShouldBe(0);
        reply.ResultText().ShouldContain($"Added 1 commits to {key} (0 already there).");
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM work_item_commit c JOIN work_item w ON w.id = c.work_item_id WHERE w.key = @key AND c.archived_at IS NULL",
            new { key })).ShouldBe(1);
    }

    [Test]
    public async Task AddCommitsTool_WhenCommitUrlGivenWithoutRepo_ShouldAdd()
    {
        var key = await NewItemKeyAsync("bug");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "add_commits", Args(key, $$"""{"sha":"https://github.com/acme/payments/commit/{{Shas.PaymentsOnly}}"}"""));

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var added = reply.Structured().GetProperty("added").EnumerateArray().Single();
        added.GetProperty("repo").GetString().ShouldBe(PaymentsRepo);
        added.GetProperty("sha").GetString().ShouldBe(Shas.PaymentsOnly);
    }

    [Test]
    public async Task AddCommitsTool_WhenSomeAlreadyThere_ShouldListSkipped()
    {
        var key = await NewItemKeyAsync("task");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        (await CallToolAsync(token, "add_commits", Args(key, Commit(StorefrontHost, Shas.First)))).IsToolError().ShouldBeFalse();

        var reply = await CallToolAsync(token, "add_commits", Args(key, Commit(StorefrontHost, Shas.First), Commit(StorefrontHost, Shas.Second)));

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        result.GetProperty("added").EnumerateArray().Select(c => c.GetProperty("sha").GetString()).ShouldBe([Shas.Second]);
        result.GetProperty("skipped").EnumerateArray().Select(c => c.GetProperty("sha").GetString()).ShouldBe([Shas.First]);
        reply.ResultText().ShouldContain($"Added 1 commits to {key} (1 already there).");
    }

    [Test]
    public async Task AddCommitsTool_WhenStory_ShouldReturnNotTaskOrBugNamingTasks()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").InsertAsync(connection);
        var task = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithParent(story).InsertAsync(connection);
        var storyKey = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
        var taskKey = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @task", new { task });
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "add_commits", Args(storyKey!, Commit(StorefrontHost, Shas.First)));

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotTaskOrBug);
        error.GetProperty("message").GetString()!.ShouldContain(taskKey!);
    }

    [Test]
    public async Task AddCommitsTool_WhenRepoUnreadable_ShouldReturnRepoUnreadable()
    {
        var key = await NewItemKeyAsync("task");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "add_commits", Args(key, Commit(StorefrontHost, Shas.First), Commit("github.com/acme/secret", Shas.First)));

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.RepoUnreadable);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM work_item_commit c JOIN work_item w ON w.id = c.work_item_id WHERE w.key = @key", new { key })).ShouldBe(0);
    }

    [Test]
    public async Task AddCommitsTool_WhenShaMissing_ShouldReturnBadSha()
    {
        var key = await NewItemKeyAsync("task");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "add_commits", Args(key, Commit(StorefrontHost, Shas.Missing)));

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.BadSha);
    }

    private async Task<string> NewItemKeyAsync(string type)
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).InsertAsync(connection);
        return (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }))!;
    }

    private static string Commit(string repo, string sha) => $$"""{"repo":"{{repo}}","sha":"{{sha}}"}""";

    private static string Args(string key, params string[] commits) => $$"""{"item":"{{key}}","commits":[{{string.Join(",", commits)}}]}""";
}
