using Dapper;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Identity;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// Log line times of an import in the viewer's zone. Joel is the only admin of the project the import goes into, so the tests run in order: the fallback first, then the zone.
[TestFixture]
public class ImportJobViewerZoneTests : ImportJobsFixture
{
    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("ZONE-1")];

    // Three seconds past two minutes after midnight UTC on the day LateEveningUtc falls on.
    private static DateTimeOffset LoggedAt => ViewerZoneTestData.LateEveningUtc.AddHours(-20).AddMinutes(2).AddSeconds(5);

    private async Task<string> FirstLogTimeAsync(string key)
    {
        var job = await StartAsync(key);
        await WaitForAsync(job.Id, ImportJobState.Done);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE import_log SET logged_at = @at WHERE import_job_id = @id", new { at = LoggedAt, id = job.Id });

        return (await LogAsync(job.Id)).First().TimeLabel;
    }

    [Test, Order(1)]
    public async Task ImportJob_WhenViewerHasNoZone_ShouldStampLogLinesInUtc()
    {
        (await FirstLogTimeAsync("ZONEA")).ShouldBe("00:02:05");
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to LogTime in ImportJobRows.
    [Test, Order(2)]
    public async Task ImportJob_WhenViewerIsInBangkok_ShouldStampLogLinesInBangkokTime()
    {
        await ViewerZoneArrange.SetAsync(Api, TestUser.Joel, ViewerZoneTestData.BangkokId);

        (await FirstLogTimeAsync("ZONEB")).ShouldBe("07:02:05");
    }
}
