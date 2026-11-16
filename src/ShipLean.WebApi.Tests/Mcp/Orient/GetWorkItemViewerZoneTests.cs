using Dapper;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Orient;

// get_work_item labels a Feature's "verified" line in the zone of the agent's user (D5); the ISO values are kept. Joel reads
// in Asia/Bangkok, Priya has no zone and reads in UTC. The Feature is stamped at ViewerZoneTestData.LateEveningUtc.
[TestFixture]
public class GetWorkItemViewerZoneTests : McpFixture
{
    private string _key = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeFeatureAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Zone tool read").InsertAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE work_item SET status_line = 'On track', status_verified_at = @at, status_verified_by = @joel, status_verified_via = 'set_status' WHERE id = @feature",
            new { at = ViewerZoneTestData.LateEveningUtc, joel = SeedIds.Users.Joel, feature });
        _key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature })
            ?? throw new InvalidOperationException("The arranged Feature has no key.");
        await ViewerZoneArrange.SetAsync(Api, TestUser.Joel, ViewerZoneTestData.BangkokId);
    }

    private async Task<string?> VerifiedLabelAsync(TestUser user)
    {
        var reply = await CallToolAsync(await TokenAsync(user, ReadOnly), "get_work_item", $$"""{"key":"{{_key}}"}""");
        return reply.Structured().GetProperty("verified").GetProperty("label").GetString();
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone in VerifiedLine.Of.
    [Test]
    public async Task GetWorkItem_WhenAgentsUserIsInBangkok_ShouldLabelVerifiedByTheBangkokDay()
    {
        var label = await VerifiedLabelAsync(TestUser.Joel);

        var expected = DisplayDate.Relative(ViewerZoneTestData.LateEveningUtc, TimeProvider.System.GetUtcNow(), ViewerZoneTestData.Bangkok);
        expected.ShouldNotBe(DisplayDate.Relative(ViewerZoneTestData.LateEveningUtc, TimeProvider.System.GetUtcNow(), TimeZoneInfo.Utc));
        label.ShouldBe(expected);
    }

    // The fallback: an agent whose user has no zone still gets UTC labels.
    [Test]
    public async Task GetWorkItem_WhenAgentsUserHasNoZone_ShouldLabelVerifiedByTheUtcDay()
    {
        var label = await VerifiedLabelAsync(TestUser.Priya);

        label.ShouldBe(DisplayDate.Relative(ViewerZoneTestData.LateEveningUtc, TimeProvider.System.GetUtcNow(), TimeZoneInfo.Utc));
    }
}
