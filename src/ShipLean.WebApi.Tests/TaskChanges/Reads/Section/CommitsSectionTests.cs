using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.WebApi.Tests.TaskChanges.Reads.Section;

// GET api/work-items/{id}/commits on the real host and database, from rows written with CodeChangesSeed.
[TestFixture]
public class CommitsSectionTests : ApiFixture
{
    private static readonly RepoRef Store = FakeCodeHost.StorefrontRepo;

    [Test]
    public async Task Section_WhenTwoCommitsTouchOneFile_ShouldCountItOnce()
    {
        var (item, seed, _) = await NewItemAsync();
        var first = await seed.CommitAsync(item, Store, FakeCodeHost.Shas.First);
        var second = await seed.CommitAsync(item, Store, FakeCodeHost.Shas.Second);

        var section = await GetAsync(item);

        // src/checkout.ts is in both commits: three distinct files, while additions and deletions sum per commit (5+1, 1+1).
        section.Totals.ShouldBe(new TaskCommitTotalsDto(2, 3, 6, 2));
        section.Commits.Select(c => c.Id).ShouldBe([first, second]);
        section.FilePaths.Select(f => f.Path).ShouldBe(["src/cart.ts", "src/checkout.ts", "src/basket.ts"]);
        section.HeadCommitId.ShouldBe(second.ToString());
        section.Commits.ShouldAllBe(c => c.Access == "ok");
    }

    [Test]
    public async Task Section_WhenArchivedCommit_ShouldLeaveItOut()
    {
        var (item, seed, connection) = await NewItemAsync();
        var first = await seed.CommitAsync(item, Store, FakeCodeHost.Shas.First);
        var second = await seed.CommitAsync(item, Store, FakeCodeHost.Shas.Second);
        await connection.ExecuteAsync("UPDATE work_item_commit SET archived_at = now() WHERE id = @second", new { second });

        var section = await GetAsync(item);

        section.Commits.Select(c => c.Id).ShouldBe([first]);
        section.Totals.ShouldBe(new TaskCommitTotalsDto(1, 2, 5, 1));
        section.HeadCommitId.ShouldBe(first.ToString());
    }

    [Test]
    public async Task Section_WhenTwoRepos_ShouldKeepPathsApart()
    {
        var (item, seed, connection) = await NewItemAsync();
        await seed.CommitAsync(item, Store, FakeCodeHost.Shas.Third);
        await seed.CommitAsync(item, FakeCodeHost.Payments, FakeCodeHost.Shas.PaymentsOnly);
        // Make the payments commit touch the same path as the storefront one.
        await connection.ExecuteAsync("UPDATE code_commit SET paths = ARRAY['src/basket.ts'] WHERE sha = @sha", new { sha = FakeCodeHost.Shas.PaymentsOnly });

        var section = await GetAsync(item);

        section.Totals.Files.ShouldBe(2);
        section.FilePaths.ShouldBe([new TaskChangeFilePathDto(FakeCodeHost.Storefront, "src/basket.ts"), new TaskChangeFilePathDto(FakeCodeHost.PaymentsRepo, "src/basket.ts")]);
    }

    [Test]
    public async Task Section_WhenResolvedComment_ShouldNotCountIt()
    {
        var (item, seed, connection) = await NewItemAsync();
        await seed.CommitAsync(item, Store, FakeCodeHost.Shas.First);
        var open = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");
        var resolved = await seed.CommentAsync(item, Store, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 2, "Done?");
        await seed.ReplyAsync(item, open, "A reply is not a thread.");
        await connection.ExecuteAsync("UPDATE code_comment SET resolved_at = now() WHERE id = @resolved", new { resolved });

        (await GetAsync(item)).OpenComments.ShouldBe(1);
    }

    [Test]
    public async Task Section_WhenFetched_ShouldMatchFixtureShape()
    {
        var (item, seed, _) = await NewItemAsync();
        await seed.CommitAsync(item, Store, FakeCodeHost.Shas.First);
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "taskChanges", "section.json");
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(path));

        using var response = await Api.GetAsync($"api/work-items/{item}/commits");
        using var actual = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Shape(actual.RootElement).ShouldBe(Shape(fixture.RootElement.GetProperty("section")));
        Shape(actual.RootElement).ShouldContain("commits[].authoredAtLabel");
    }

    // Every property path in a document, arrays flattened, so two documents compare by structure and not by value.
    private static List<string> Shape(JsonElement element, string prefix = "")
    {
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        Collect(element, prefix, paths);
        return [.. paths];
    }

    private static void Collect(JsonElement element, string prefix, SortedSet<string> paths)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var name = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
                    paths.Add(name);
                    Collect(property.Value, name, paths);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, prefix + "[]", paths);
                }

                break;
        }
    }

    private async Task<TaskCommitsSectionDto> GetAsync(Guid item)
    {
        using var response = await Api.GetAsync($"api/work-items/{item}/commits");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return JsonSerializer.Deserialize<TaskCommitsSectionDto>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private async Task<(Guid Item, CodeChangesSeed Seed, NpgsqlConnection Connection)> NewItemAsync()
    {
        var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        return (item, new CodeChangesSeed(connection), connection);
    }
}
