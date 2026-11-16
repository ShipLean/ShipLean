using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.TestSupport.TaskChanges;
using ShipLean.WebApi.Tests.Mcp.Transport;
using static ShipLean.TestSupport.TaskChanges.FakeCodeHost;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Briefs;

// get_brief's commits and open code comments on a task, over the seeded plan and the fake code host, as Joel.
[TestFixture]
public class GetBriefCodeCommentsTests : McpFixture
{
    private PlanSeedKeys _keys = null!;
    private int _taken;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddFakeCodeHost();
    };

    [OneTimeSetUp]
    public async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        _keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
    }

    [Test]
    public async Task GetBrief_WhenOpenCodeComments_ShouldListThemInMarkdownAndField()
    {
        var task = NextTask();
        var id = _keys.Ids[task];
        Guid comment;
        await using (var connection = await OpenDatabaseAsync())
        {
            var seed = new CodeChangesSeed(connection);
            await seed.CommitAsync(id, StorefrontRepo, Shas.First);
            comment = await seed.CommentAsync(id, StorefrontRepo, Shas.First, "src/cart.ts", CodeSide.New, 12, "Cover the empty cart.", SeedIds.Users.Priya, lineText: "return items;");
        }

        var result = await BriefAsync(task);

        var markdown = result.GetProperty("markdown").GetString()!;
        markdown.ShouldContain("## Open code comments");
        markdown.ShouldContain($"{comment} · {Storefront} src/cart.ts:12");
        markdown.ShouldContain("Cover the empty cart.");
        markdown.ShouldContain("answered_comments");
        var field = result.GetProperty("open_code_comments").EnumerateArray().Single();
        field.GetProperty("id").GetString().ShouldBe(comment.ToString());
        field.GetProperty("repo").GetString().ShouldBe(Storefront);
        field.GetProperty("path").GetString().ShouldBe("src/cart.ts");
        field.GetProperty("line").GetInt32().ShouldBe(12);
        field.GetProperty("body").GetString().ShouldBe("Cover the empty cart.");
        result.GetProperty("commits").EnumerateArray().Single().GetProperty("sha").GetString().ShouldBe(Shas.First);
    }

    [Test]
    public async Task GetBrief_WhenResolvedComment_ShouldLeaveItOut()
    {
        var task = NextTask();
        var id = _keys.Ids[task];
        await using (var connection = await OpenDatabaseAsync())
        {
            var seed = new CodeChangesSeed(connection);
            await seed.CommitAsync(id, StorefrontRepo, Shas.First);
            var comment = await seed.CommentAsync(id, StorefrontRepo, Shas.First, "src/cart.ts", CodeSide.New, 3, "Already handled.", SeedIds.Users.Priya);
            await connection.ExecuteAsync("UPDATE code_comment SET resolved_at = now() WHERE id = @comment", new { comment });
        }

        var result = await BriefAsync(task);

        result.GetProperty("open_code_comments").GetArrayLength().ShouldBe(0);
        result.GetProperty("markdown").GetString()!.ShouldNotContain("## Open code comments");
        result.GetProperty("commits").GetArrayLength().ShouldBe(1);
    }

    [Test]
    public async Task GetBrief_WhenStoryOrFeature_ShouldHaveEmptyLists()
    {
        foreach (var key in new[] { _keys.Feature, _keys.Stories.Values.First() })
        {
            var result = await BriefAsync(key);

            result.GetProperty("commits").GetArrayLength().ShouldBe(0);
            result.GetProperty("open_code_comments").GetArrayLength().ShouldBe(0);
        }
    }

    private string NextTask() => _keys.Tasks.Values.ElementAt(_taken++);

    private async Task<System.Text.Json.JsonElement> BriefAsync(string item)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_brief", $$"""{"item":"{{item}}"}""");
        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        return reply.Structured();
    }
}
