using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TaskChanges;
using ShipLean.WebApi.Tests.Mcp.Transport;
using static ShipLean.TestSupport.TaskChanges.FakeCodeHost;

namespace ShipLean.WebApi.Tests.Mcp.Orient;

// get_work_item's commits and open_code_comments, over the fake code host, as Joel with a read token.
[TestFixture]
public class OrientCommitsTests : McpFixture
{
    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddFakeCodeHost();
    };

    [Test]
    public async Task GetWorkItem_WhenTaskHasCommits_ShouldListThem()
    {
        var (item, key) = await NewItemAsync("task");
        await using (var connection = await OpenDatabaseAsync())
        {
            var seed = new CodeChangesSeed(connection);
            await seed.CommitAsync(item, StorefrontRepo, Shas.First);
            await seed.CommitAsync(item, Payments, Shas.PaymentsOnly);
            await seed.CommentAsync(item, StorefrontRepo, Shas.First, "src/cart.ts", CodeSide.Old, 7, "Why drop this?", SeedIds.Users.Priya, lineText: "const total = 0;");
        }

        var result = await ReadAsync(key);

        var commits = result.GetProperty("commits").EnumerateArray().ToList();
        commits.Select(c => (c.GetProperty("repo").GetString(), c.GetProperty("sha").GetString())).ShouldBe([(Storefront, Shas.First), (PaymentsRepo, Shas.PaymentsOnly)]);
        var meta = MetaOf(Storefront, Shas.First);
        commits[0].GetProperty("subject").GetString().ShouldBe(meta.Subject);
        commits[0].GetProperty("additions").GetInt32().ShouldBe(meta.Additions);
        commits[0].GetProperty("deletions").GetInt32().ShouldBe(meta.Deletions);
        var comment = result.GetProperty("open_code_comments").EnumerateArray().Single();
        comment.GetProperty("repo").GetString().ShouldBe(Storefront);
        comment.GetProperty("path").GetString().ShouldBe("src/cart.ts");
        comment.GetProperty("side").GetString().ShouldBe("old");
        comment.GetProperty("line").GetInt32().ShouldBe(7);
        comment.GetProperty("sha").GetString().ShouldBe(Shas.First);
        comment.GetProperty("line_text").GetString().ShouldBe("const total = 0;");
        comment.GetProperty("body").GetString().ShouldBe("Why drop this?");
        comment.GetProperty("by").GetString().ShouldNotBeNullOrEmpty();
        Guid.Parse(comment.GetProperty("id").GetString()!).ShouldNotBe(Guid.Empty);
    }

    [Test]
    public async Task GetWorkItem_WhenBugHasNoCommits_ShouldHaveEmptyLists()
    {
        var (_, key) = await NewItemAsync("bug");

        var result = await ReadAsync(key);

        result.GetProperty("commits").GetArrayLength().ShouldBe(0);
        result.GetProperty("open_code_comments").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task GetWorkItem_WhenStory_ShouldHaveEmptyCommits()
    {
        var (item, key) = await NewItemAsync("story");
        await using (var connection = await OpenDatabaseAsync())
        {
            var seed = new CodeChangesSeed(connection);
            await seed.CommitAsync(item, StorefrontRepo, Shas.First);
            await seed.CommentAsync(item, StorefrontRepo, Shas.First, "src/cart.ts", CodeSide.New, 1, "Stray");
        }

        var result = await ReadAsync(key);

        result.GetProperty("commits").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);
        result.GetProperty("commits").GetArrayLength().ShouldBe(0);
        result.GetProperty("open_code_comments").GetArrayLength().ShouldBe(0);
    }

    private async Task<System.Text.Json.JsonElement> ReadAsync(string key)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");
        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        return reply.Structured();
    }

    private async Task<(Guid Id, string Key)> NewItemAsync(string type)
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).InsertAsync(connection);
        return (id, (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }))!);
    }
}
