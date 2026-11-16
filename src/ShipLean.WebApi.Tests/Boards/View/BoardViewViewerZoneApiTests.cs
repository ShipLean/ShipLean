using System.Globalization;
using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;

namespace ShipLean.WebApi.Tests.Boards.View;

// The Created card field in the viewer's zone. Its own class so the board settings it rewrites and the zone it sets stay out of the other board tests.
[TestFixture]
public class BoardViewViewerZoneApiTests : ApiFixture
{
    private static readonly Guid Board = SeedIds.Boards.ShipStories;

    private async Task ShowCreatedOnStoryCardsAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            """
            UPDATE board SET settings = jsonb_set(settings, '{cardFields}',
                '[{"type":"story","showKey":true,"showAssignee":true,"showTags":false,"showPoints":true,"showEmptyFields":false,"extraFields":["createdAt"]}]'::jsonb)
            WHERE id = @board
            """,
            new { board = Board });
        await connection.ExecuteAsync("UPDATE work_item SET created_at = @at WHERE id = @id", new { at = ViewerZoneTestData.LateEveningUtc, id = SeedIds.WorkItems.Ship129 });
    }

    private async Task<string?> CreatedLabelAsync(TestUser user)
    {
        var board = await Api.ActingAs(user).BoardAsync(Board);
        return board.Cards().Single(c => c.Key == "SHIP-129").ExtraFields.Single(f => f.FieldId == "createdAt").Text;
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the Created Relative call in CardExtraFields.
    [Test]
    public async Task Board_WhenViewerIsInBangkok_ShouldLabelTheCreatedFieldByTheBangkokDay()
    {
        await ShowCreatedOnStoryCardsAsync();
        await ViewerZoneArrange.SetAsync(Api, TestUser.Priya, ViewerZoneTestData.BangkokId);

        var label = await CreatedLabelAsync(TestUser.Priya);

        // 20:00 UTC is 03:00 the next day in Bangkok; two years old, so the label is the short day with its year.
        var bangkokDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7)).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var utcDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        bangkokDay.ShouldNotBe(utcDay);
        label.ShouldBe(bangkokDay);
    }

    [Test]
    public async Task Board_WhenViewerHasNoZone_ShouldLabelTheCreatedFieldByTheUtcDay()
    {
        await ShowCreatedOnStoryCardsAsync();

        var label = await CreatedLabelAsync(TestUser.Tom);

        label.ShouldBe(ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture));
    }
}
