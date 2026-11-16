using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Orient;

// get_work_item labels a Feature's "verified" line in the zone of the agent's user (D5); the ISO values are kept. Joel reads
// in Asia/Bangkok, Priya has no zone and reads in UTC. The Feature is stamped at a fixed 20:00 UTC instant.
[TestFixture]
public class GetWorkItemViewerZoneTests : McpFixture
{
    // 20:00 UTC on Sun 10 Mar 2024: Bangkok reads 03:00 on Mon 11 Mar. The expected labels below are literals.
    private static readonly DateTimeOffset Evening = new(2024, 3, 10, 20, 0, 0, TimeSpan.Zero);

    private string _key = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeFeatureAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Zone tool read").InsertAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE work_item SET status_line = 'On track', status_verified_at = @at, status_verified_by = @joel, status_verified_via = 'set_status' WHERE id = @feature",
            new { at = Evening, joel = SeedIds.Users.Joel, feature });
        await new CommentBuilder(feature).WithAuthor(SeedIds.Users.Joel).WithBody("Zone comment").WithCreatedAt(Evening).InsertAsync(connection);
        _key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature })
            ?? throw new InvalidOperationException("The arranged Feature has no key.");
        await ViewerZoneArrange.SetAsync(Api, TestUser.Joel, ViewerZoneTestData.BangkokId);
    }

    private async Task<System.Text.Json.JsonElement> ReadAsync(TestUser user)
    {
        var reply = await CallToolAsync(await TokenAsync(user, ReadOnly), "get_work_item", $$"""{"key":"{{_key}}"}""");
        return reply.Structured();
    }

    private async Task<string?> VerifiedLabelAsync(TestUser user) =>
        (await ReadAsync(user)).GetProperty("verified").GetProperty("label").GetString();

    private async Task<string?> CommentLabelAsync(TestUser user) =>
        (await ReadAsync(user)).GetProperty("comments")[0].GetProperty("at_label").GetString();

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone in VerifiedLine.Of.
    [Test]
    public async Task GetWorkItem_WhenAgentsUserIsInBangkok_ShouldLabelVerifiedByTheBangkokDay()
    {
        var label = await VerifiedLabelAsync(TestUser.Joel);

        label.ShouldBe("11 Mar 2024");
    }

    // Mutation: build the label with TimeZoneInfo.Utc in GetWorkItemTool.
    [Test]
    public async Task GetWorkItem_WhenAgentsUserIsInBangkok_ShouldLabelACommentByTheBangkokDay()
    {
        var label = await CommentLabelAsync(TestUser.Joel);

        label.ShouldBe("11 Mar 2024");
    }

    [Test]
    public async Task GetWorkItem_WhenAgentsUserHasNoZone_ShouldLabelACommentByTheUtcDay()
    {
        var label = await CommentLabelAsync(TestUser.Priya);

        label.ShouldBe("10 Mar 2024");
    }

    // The fallback: an agent whose user has no zone still gets UTC labels.
    [Test]
    public async Task GetWorkItem_WhenAgentsUserHasNoZone_ShouldLabelVerifiedByTheUtcDay()
    {
        var label = await VerifiedLabelAsync(TestUser.Priya);

        label.ShouldBe("10 Mar 2024");
    }
}
