using System.Globalization;
using Dapper;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;

namespace ShipLean.WebApi.Tests.Taskboard;

// The Created card field of a taskboard card in the viewer's zone. Its own class so the card settings it rewrites and the zone it sets stay out of the other taskboard tests.
[TestFixture]
public class TaskboardViewerZoneApiTests : ApiFixture
{
    // A viewer of its own, a member of SHIP: the seeded users' zones are never touched.
    private async Task<TestUser> ArrangeViewerAsync(string? zone)
    {
        TestUser viewer;
        await using (var connection = await OpenDatabaseAsync())
        {
            var id = await new AppUserBuilder().InsertAsync(connection);
            await connection.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @project, @user, 'member')",
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, user = id });
            viewer = TestUser.Of(id);
        }

        if (zone is not null)
        {
            await ViewerZoneArrange.SetAsync(Api, viewer, zone);
        }

        return viewer;
    }

    private async Task<string?> CreatedLabelAsync(TestUser user, string sprintName)
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, sprintName);
        await connection.ExecuteAsync(
            """
            UPDATE board SET settings = jsonb_set(settings, '{cardFields}',
                '[{"type":"story","showKey":true,"showAssignee":true,"showTags":false,"showPoints":true,"showEmptyFields":false,"extraFields":["createdAt"]}]'::jsonb)
            WHERE id = @board
            """,
            new { board = SeedIds.Boards.ShipTaskboard });
        await connection.ExecuteAsync("UPDATE work_item SET created_at = @at WHERE id = @id", new { at = ViewerZoneTestData.LateEveningUtc, id = board.Story });

        var view = await Api.ActingAs(user).GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");

        return view.Rows.Single(r => r.Parent?.Id == board.Story).Parent!.ExtraFields.Single(f => f.FieldId == "createdAt").Text;
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the Created Relative call in TaskboardCards.
    [Test]
    public async Task Taskboard_WhenViewerIsInBangkok_ShouldLabelTheCreatedFieldByTheBangkokDay()
    {
        var viewer = await ArrangeViewerAsync(ViewerZoneTestData.BangkokId);

        var label = await CreatedLabelAsync(viewer, "Bangkok sprint");

        // 20:00 UTC is 03:00 the next day in Bangkok; two years old, so the label is the short day with its year.
        var bangkokDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7)).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var utcDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        bangkokDay.ShouldNotBe(utcDay);
        label.ShouldBe(bangkokDay);
    }

    [Test]
    public async Task Taskboard_WhenViewerHasNoZone_ShouldLabelTheCreatedFieldByTheUtcDay()
    {
        var label = await CreatedLabelAsync(await ArrangeViewerAsync(null), "No zone sprint");

        label.ShouldBe(ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture));
    }
}
