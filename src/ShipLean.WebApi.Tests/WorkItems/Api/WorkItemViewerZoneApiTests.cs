using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;

namespace ShipLean.WebApi.Tests.WorkItems.Api;

// The work item read dates its labels in the viewer's zone. One Feature stamped at 20:00 UTC (the next day in Bangkok); Joel reads in Asia/Bangkok, Priya has no zone and reads in UTC. The instant is fixed and the
// expected labels are literals, not DisplayDate output.
[TestFixture]
public class WorkItemViewerZoneApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    // 20:00 UTC on Sun 10 Mar 2024: Bangkok reads 03:00 on Mon 11 Mar. The year is always an earlier one, so labels are "d MMM yyyy".
    private static readonly DateTimeOffset Evening = new(2024, 3, 10, 20, 0, 0, TimeSpan.Zero);

    private Guid _feature;

    [OneTimeSetUp]
    public async Task ArrangeFeatureAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        _feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Zone read").InsertAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE work_item SET status_line = 'On track', status_verified_at = @at, status_verified_by = @joel, status_verified_via = 'set_status', started_on = '2026-01-05', health = 'onTrack', health_set_at = @at, health_set_by = @joel WHERE id = @_feature",
            new { at = Evening, joel = SeedIds.Users.Joel, _feature });
        await ViewerZoneArrange.SetAsync(Api, TestUser.Joel, ViewerZoneTestData.BangkokId);
    }

    private async Task<WorkItemDto> ReadAsync(TestUser user)
    {
        using var response = await Api.ActingAs(user).GetAsync($"api/work-items/{_feature}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<WorkItemDto>(text, Wire)!;
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the Relative calls in WorkItemReads.
    [Test]
    public async Task Get_WhenViewerIsInBangkok_ShouldDateVerifiedAndHealthByTheBangkokDay()
    {
        var item = await ReadAsync(TestUser.Joel);

        item.StatusVerified!.AtLabel.ShouldBe("11 Mar 2024");
        item.HealthSet!.AtLabel.ShouldBe("11 Mar 2024");
    }

    // The fallback: a user with no zone still reads UTC labels.
    [Test]
    public async Task Get_WhenViewerHasNoZone_ShouldDateVerifiedByTheUtcDay()
    {
        var item = await ReadAsync(TestUser.Priya);

        item.StatusVerified!.AtLabel.ShouldBe("10 Mar 2024");
    }
}
