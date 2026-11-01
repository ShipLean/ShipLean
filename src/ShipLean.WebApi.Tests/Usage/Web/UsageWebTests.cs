using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Usage;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Usage.Web;

// What the web action filter tracks. The host's IUsageEvents is the recorder, so every assertion reads what the filter handed it.
[TestFixture]
public class UsageWebTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    private UsageEvent Only(string source) => Recorder.Events.Where(e => e.Source == source).ShouldHaveSingleItem();

    private async Task<WriteResult> CreateStoryAsync(string title)
    {
        using var response = await Api.PostJsonAsync("api/work-items", new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = "story",
            ["title"] = title,
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = null,
        });
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<WriteResult>(text, PersistedJson.Options)!;
    }

    [Test]
    public async Task CreateWorkItem_WhenUserCreates_ShouldTrackWorkItemCreatedWithProject()
    {
        await CreateStoryAsync("Tracked create");

        var tracked = Only("api:WorkItems.Create");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemCreated);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
        tracked.ErrorCode.ShouldBeNull();
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.ProjectCount.ShouldBe(1);
        tracked.DurationMs.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task PatchWorkItem_WhenStaleRevision_ShouldTrackErrorWithConflict()
    {
        var created = await CreateStoryAsync("Tracked stale patch");

        using var response = await Api.PatchJsonAsync(
            $"api/work-items/{created.Id}",
            new JsonObject { ["expectedRevision"] = created.Revision + 5, ["changes"] = new JsonObject { ["title"] = new JsonObject { ["value"] = "Other" } } });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var tracked = Only("api:WorkItems.Patch");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemUpdated);
        tracked.Outcome.ShouldBe(UsageOutcome.Error);
        tracked.ErrorCode.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task GetWorkItem_WhenNotMember_ShouldTrackErrorWithNotFound()
    {
        Guid itemId;
        string itemKey;
        Guid projectId;
        await using (var connection = await OpenDatabaseAsync())
        {
            var project = await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Priya, ProjectRole.Member));
            projectId = project.Id;
            itemKey = $"{project.Key}-1";
            itemId = await new WorkItemBuilder(project.Id).WithType("story").WithTitle("Not yours").InsertAsync(connection);
        }

        using var response = await Api.GetAsync($"api/projects/{projectId}/work-items/by-key/{itemKey}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        itemId.ShouldNotBe(Guid.Empty);
        var tracked = Only("api:ProjectWorkItems.ByKey");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemViewed);
        tracked.Outcome.ShouldBe(UsageOutcome.Error);
        tracked.ErrorCode.ShouldBe(ApiErrorCodes.NotFound);
        tracked.ProjectId.ShouldBeNull();
    }

    [Test]
    public async Task BoardMove_WhenMoved_ShouldTrackWorkItemMovedWithBoardProp()
    {
        Guid id;
        await using (var connection = await OpenDatabaseAsync())
        {
            id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Action to move").WithParent(SeedIds.WorkItems.Ship104).WithBoardPosition("col-new").InsertAsync(connection);
        }

        using var response = await Api.PostJsonAsync($"api/boards/{SeedIds.Boards.ShipStories}/moves", new { itemId = id, expectedRevision = 1, columnId = "col-done", done = false, laneId = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tracked = Only("api:BoardView.Move");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemMoved);
        tracked.Properties[UsageProps.Board].ShouldBe("board");
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
    }

    [Test]
    public async Task Logout_ShouldTrackSignedOutWithThatUser()
    {
        using var response = await Api.PostJsonAsync("api/auth/logout", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Only("api:Auth.Logout");
        tracked.Name.ShouldBe(UsageEventNames.AuthSignedOut);
        tracked.Actor.UserId.ShouldBe(SeedIds.Users.Joel);
        tracked.ProjectId.ShouldBeNull();
    }

    [Test]
    public async Task UntrackedAction_WhenCalled_ShouldTrackNothing()
    {
        using var response = await Api.PostJsonAsync("api/markdown/render", new { markdown = "# Hello" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task GetBoard_ShouldTrackNothing()
    {
        using var response = await Api.GetAsync($"api/boards/{SeedIds.Boards.ShipStories}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task SelfEmittedAction_ShouldNotBeTrackedByTheFilter()
    {
        using var screen = await Api.PostJsonAsync("api/usage/screen", new ScreenViewDto("board", null));
        using var logout = await Api.PostJsonAsync("api/auth/logout", new { });

        screen.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Recorder.Events.ShouldNotContain(e => e.Source == "api:Usage.Screen");
        Only("api:Auth.Logout").Name.ShouldBe(UsageEventNames.AuthSignedOut);
    }
}
