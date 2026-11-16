using Dapper;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Identity;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// Log line times of an import in the viewer's zone. Each test acts as a viewer of its own who administers the project the connection belongs to.
[TestFixture]
public class ImportJobViewerZoneTests : ImportJobsFixture
{
    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-1")];

    // Three seconds past two minutes after midnight UTC on the day LateEveningUtc falls on.
    private static DateTimeOffset LoggedAt => ViewerZoneTestData.LateEveningUtc.AddHours(-20).AddMinutes(2).AddSeconds(5);

    // A viewer of its own, an admin of the project: the seeded users' zones are never touched.
    private async Task<TestUser> ArrangeViewerAsync(string? zone)
    {
        TestUser viewer;
        await using (var connection = await OpenDatabaseAsync())
        {
            var id = await new AppUserBuilder().InsertAsync(connection);
            await connection.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @project, @user, 'admin')",
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Empt, user = id });
            viewer = TestUser.Of(id);
        }

        if (zone is not null)
        {
            await ViewerZoneArrange.SetAsync(Api, viewer, zone);
        }

        return viewer;
    }

    private async Task<string> FirstLogTimeAsync(string key, TestUser viewer)
    {
        var job = await StartAsync(key, api: Api.ActingAs(viewer));
        await WaitForAsync(job.Id, ImportJobState.Done);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE import_log SET logged_at = @at WHERE import_job_id = @id", new { at = LoggedAt, id = job.Id });

        return (await ReadAsync<IReadOnlyList<ImportLogLineDto>>(await Api.ActingAs(viewer).GetAsync($"/api/import/jobs/{job.Id}/log")))[0].TimeLabel;
    }

    [Test]
    public async Task ImportJob_WhenViewerHasNoZone_ShouldStampLogLinesInUtc()
    {
        (await FirstLogTimeAsync("ZNAA", await ArrangeViewerAsync(null))).ShouldBe("00:02:05");
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to LogTime in ImportJobRows.
    [Test]
    public async Task ImportJob_WhenViewerIsInBangkok_ShouldStampLogLinesInBangkokTime()
    {
        var viewer = await ArrangeViewerAsync(ViewerZoneTestData.BangkokId);

        (await FirstLogTimeAsync("ZNAB", viewer)).ShouldBe("07:02:05");
    }
}
