using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;

namespace ShipLean.WebApi.Tests.Timeline;

// The Feature timeline labels its bars and key dates in the viewer's zone. The Feature is added at ViewerZoneTestData.LateEveningUtc
// (20:00 UTC, the next day in Bangkok) and its one task starts the next UTC day at 00:02 (07:02 in Bangkok). One dedicated viewer reads in
// Asia/Bangkok, another has no zone and reads in UTC; both are members of the project and no seeded user's zone is touched. The expected text is typed from the +07:00 offset, not built with DisplayDate.
[TestFixture]
public class FeatureTimelineViewerZoneTests : ApiFixture
{
    private static readonly Guid Ship = SeedIds.Projects.Ship;

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static DateTimeOffset TaskStart => new(ViewerZoneTestData.LateEveningUtc.UtcDateTime.Date.AddDays(1).AddMinutes(2), TimeSpan.Zero);

    private string _featureKey = string.Empty;
    private TestUser _bangkokViewer = TestUser.Anonymous;
    private TestUser _noZoneViewer = TestUser.Anonymous;

    [OneTimeSetUp]
    public async Task ArrangeFeatureAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(Ship).WithType("feature").WithCreatedAt(ViewerZoneTestData.LateEveningUtc).WithCreatedBy(SeedIds.Users.Joel).InsertAsync(connection);
        var story = await new WorkItemBuilder(Ship).WithType("story").WithParent(feature).WithCreatedAt(ViewerZoneTestData.LateEveningUtc).WithCreatedBy(SeedIds.Users.Joel).InsertAsync(connection);
        var task = await new WorkItemBuilder(Ship).WithType("task").WithParent(story).WithCreatedAt(ViewerZoneTestData.LateEveningUtc).WithCreatedBy(SeedIds.Users.Joel).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = ANY(@ids) AND rev = 1", new { at = ViewerZoneTestData.LateEveningUtc, ids = new[] { feature, story, task } });
        await new RevisionBuilder(task).WithRev(2).WithChangedAt(TaskStart).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{SeedIds.States.Ship.TaskActive}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = 2 WHERE id = @task", new { task, state = SeedIds.States.Ship.TaskActive });
        _featureKey = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature }) ?? throw new InvalidOperationException();
        _bangkokViewer = await ArrangeViewerAsync(connection);
        _noZoneViewer = await ArrangeViewerAsync(connection);
        await ViewerZoneArrange.SetAsync(Api, _bangkokViewer, ViewerZoneTestData.BangkokId);
    }

    private static async Task<TestUser> ArrangeViewerAsync(Npgsql.NpgsqlConnection connection)
    {
        var id = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @project, @user, 'member')",
            new { id = Guid.NewGuid(), project = Ship, user = id });
        return TestUser.Of(id);
    }

    private async Task<FeatureTimelineViewDto> ReadAsync(TestUser user)
    {
        using var response = await Api.ActingAs(user).GetAsync($"api/projects/{Ship}/work-items/by-key/{_featureKey}/timeline");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<FeatureTimelineViewDto>(text, Wire)!;
    }

    // "Mon 5 Oct" from the instant's own offset: the weekday and month names by hand, no format string.
    private static string Day(DateTimeOffset at)
    {
        var names = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat;
        return $"{names.AbbreviatedDayNames[(int)at.DayOfWeek]} {at.Day} {names.AbbreviatedMonthNames[at.Month - 1]}";
    }

    // "07:02": the hour and minute of the instant's own offset.
    private static string Clock(DateTimeOffset at) => $"{at.Hour:D2}:{at.Minute:D2}";

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to TimelineBuilder.Build in FeatureTimeline.
    [Test]
    public async Task FeatureTimeline_WhenViewerIsInBangkok_ShouldLabelBarsInBangkokTime()
    {
        var view = await ReadAsync(_bangkokViewer);

        var bangkokStart = TaskStart.ToOffset(TimeSpan.FromHours(7));
        var period = view.Bars.Single().History[0];
        period.FromLabel.ShouldBe($"{Day(bangkokStart)}, {Clock(bangkokStart)}");
        var added = view.KeyDates.Single(date => date.Point == KeyDatePoint.Added);
        added.DayLabel.ShouldBe(Day(ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7))));
        added.DayLabel.ShouldNotBe(Day(ViewerZoneTestData.LateEveningUtc));
    }

    // The fallback: a user with no zone still reads UTC labels.
    [Test]
    public async Task FeatureTimeline_WhenViewerHasNoZone_ShouldLabelBarsInUtc()
    {
        var view = await ReadAsync(_noZoneViewer);

        view.Bars.Single().History[0].FromLabel.ShouldBe($"{Day(TaskStart)}, {Clock(TaskStart)}");
        view.KeyDates.Single(date => date.Point == KeyDatePoint.Added).DayLabel.ShouldBe(Day(ViewerZoneTestData.LateEveningUtc));
    }
}
