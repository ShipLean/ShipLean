using Dapper;
using ShipLean.Contracts.Backlog;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Builders;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// The review field of the Stories backlog read: each check lists its rows by id, over the whole stack, and agrees with review_backlog.
// Every test arranges rows with words no other test uses, at the top of the stack, and asserts on its own ids only.
[TestFixture]
public class BacklogReviewApiTests : McpFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog";
    private const string TagFilter = """{"match":"all","clauses":[{"field":"tags","op":"contains","value":"Payments"}]}""";

    private string _bearer = string.Empty;

    protected override bool WithProbeTools => false;

    protected override TestUser SignedInAs => TestUser.Joel;

    [OneTimeSetUp]
    public async Task IssueAToken() => _bearer = await TokenAsync(TestUser.Joel, ReadWritePlan);

    [Test]
    public async Task Get_WhenRowUnestimated_ShouldListItUnderUnestimated()
    {
        var row = await InsertAsync(b => b.WithTitle("Unestimated lemur ledger"));
        var estimated = await InsertAsync(b => b.WithTitle("Estimated lemur ledger quartz").WithPoints(3));

        var review = await ReviewAsync("list");

        Ids(review, "unestimated").ShouldContain(row);
        Ids(review, "unestimated").ShouldNotContain(estimated);
    }

    [Test]
    public async Task Get_WhenRowHasNoCriteria_ShouldListItUnderNoAcceptanceCriteria()
    {
        var bare = await InsertAsync(b => b.WithTitle("Bare ocelot harbour").WithPoints(2));
        var covered = await InsertAsync(b => b.WithTitle("Covered pangolin archive").WithPoints(2));
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE work_item SET acceptance_criteria = '[{\"text\":\"Done\",\"done\":false}]'::jsonb WHERE id = @covered", new { covered });

        var review = await ReviewAsync("planning");

        Ids(review, "no_acceptance_criteria").ShouldContain(bare);
        Ids(review, "no_acceptance_criteria").ShouldNotContain(covered);
    }

    [Test]
    public async Task Get_WhenRowHasNoParent_ShouldListItUnderNoParent()
    {
        var feature = await InsertAsync(b => b.WithType("feature").WithTitle("Parent for the walrus"));
        var orphan = await InsertAsync(b => b.WithTitle("Orphan walrus beacon"));
        var child = await InsertAsync(b => b.WithTitle("Child tapir granite").WithParent(feature));

        var review = await ReviewAsync("list");

        Ids(review, "no_parent").ShouldContain(orphan);
        Ids(review, "no_parent").ShouldNotContain(child);
    }

    [Test]
    public async Task Get_WhenRowUntouchedForDays_ShouldListItUnderStale()
    {
        var old = await InsertAsync(b => b.WithTitle("Stale heron lantern"));
        var fresh = await InsertAsync(b => b.WithTitle("Fresh badger compass"));
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE work_item SET changed_at = now() - interval '90 days' WHERE id = @old", new { old });

        var review = await ReviewAsync("list");

        Ids(review, "stale").ShouldContain(old);
        Ids(review, "stale").ShouldNotContain(fresh);
    }

    [Test]
    public async Task Get_WhenTitlesShareWords_ShouldListBothUnderLikelyDuplicate()
    {
        var first = await InsertAsync(b => b.WithTitle("Zebra quokka exporter dark toggle"));
        var second = await InsertAsync(b => b.WithTitle("Quokka exporter zebra toggle for dark"));
        var other = await InsertAsync(b => b.WithTitle("Marmoset ledger reconciliation"));

        var review = await ReviewAsync("list");

        Ids(review, "likely_duplicate").ShouldContain(first);
        Ids(review, "likely_duplicate").ShouldContain(second);
        Ids(review, "likely_duplicate").ShouldNotContain(other);
    }

    [Test]
    public async Task Get_WhenFilterOn_ShouldStillCheckTheWholeStack()
    {
        var row = await InsertAsync(b => b.WithTitle("Filtered-out civet vault"));

        var view = await Api.GetBacklogAsync($"{Url}?mode=list&filter={Uri.EscapeDataString(TagFilter)}");

        view.Rows.Select(r => r.Id).ShouldNotContain(row);
        Ids(view.Review.ShouldNotBeNull(), "unestimated").ShouldContain(row);
    }

    [Test]
    public async Task Get_WhenClosedRow_ShouldNotBeChecked()
    {
        var closed = await InsertAsync(b => b.WithTitle("Closed gecko prism").WithStateNamed("Closed"));
        var open = await InsertAsync(b => b.WithTitle("Open gecko prism rudder"));

        var review = await ReviewAsync("list");

        Ids(review, "unestimated").ShouldContain(open);
        review.Checks.SelectMany(c => c.ItemIds).ShouldNotContain(closed);
    }

    [Test]
    public async Task Get_WhenSameInputs_ShouldAgreeWithReviewBacklogTool()
    {
        await InsertAsync(b => b.WithTitle("Agreement lynx prairie"));
        await InsertAsync(b => b.WithTitle("Agreement stoat meadow").WithPoints(5));

        var review = await ReviewAsync("planning");

        var reply = await CallToolAsync(_bearer, "review_backlog", """{"project":"SHIP","top":100,"stale_days":30}""");
        reply.IsToolError().ShouldBeFalse(reply.Body);
        var items = reply.Structured().GetProperty("items").EnumerateArray().ToList();
        await using var connection = await OpenDatabaseAsync();
        var idsByKey = (await connection.QueryAsync<(string Key, Guid Id)>("SELECT key, id FROM work_item WHERE project_id = @p", new { p = SeedIds.Projects.Ship }))
            .ToDictionary(x => x.Key, x => x.Id);
        foreach (var check in review.Checks)
        {
            var fromTool = items
                .Where(i => i.GetProperty("flags").EnumerateArray().Any(f => f.GetString() == check.Id))
                .Select(i => idsByKey[i.GetProperty("key").GetString()!]);
            check.ItemIds.ShouldBe(fromTool, ignoreOrder: true, check.Id);
        }
    }

    private async Task<BacklogReviewDto> ReviewAsync(string mode)
        => (await Api.GetBacklogAsync($"{Url}?mode={mode}")).Review.ShouldNotBeNull();

    private static Guid[] Ids(BacklogReviewDto review, string check) => [.. review.Checks.Single(c => c.Id == check).ItemIds];

    // At the top of the stack, so the top 100 holds the row however many rows the other tests left.
    private async Task<Guid> InsertAsync(Func<WorkItemBuilder, WorkItemBuilder> arrange)
    {
        await using var connection = await OpenDatabaseAsync();
        return await arrange(new WorkItemBuilder(SeedIds.Projects.Ship).WithBacklogOrder("0" + Guid.NewGuid().ToString("N"))).InsertAsync(connection);
    }
}
