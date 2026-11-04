using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.Demos;

// K4 and K6's read over the real REST route, database, attachment store and blob store. Rows are set by SQL (K3), files through the attachment store.
[TestFixture]
public class SprintDemoReaderTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = WireOptions();

    private static JsonSerializerOptions WireOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed record Delivered(Guid VideoId, Guid ChaptersId, Guid ScriptId);

    private sealed record Arranged(AgentDispatchArrange Project, Guid SprintId, Guid ItemId, string ItemKey);

    private async Task<Arranged> ArrangeAsync(NpgsqlConnection database, bool agent = true, bool closed = true, bool withItem = true)
    {
        var project = await AgentDispatchArrange.CreateAsync(database, Api.ActingAs(TestUser.Joel), connection: agent, repository: agent);
        var sprintId = await DemoRunArrange.NewSprintAsync(database, project.Project.Id);
        if (closed)
        {
            await database.ExecuteAsync("UPDATE sprint SET rollover_status = 'decided' WHERE id = @sprintId", new { sprintId });
        }

        if (!withItem)
        {
            return new Arranged(project, sprintId, Guid.Empty, string.Empty);
        }

        using var scope = Host.Services.CreateScope();
        var itemId = await scope.ServiceProvider.GetRequiredService<IDemoRunStarter>().GetOrCreateItemAsync(project.Project.Id, sprintId, "Sprint 23", CancellationToken.None);
        var key = await database.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @itemId", new { itemId });
        return new Arranged(project, sprintId, itemId, key ?? string.Empty);
    }

    private async Task<SprintDemoDto> GetAsync(Guid sprintId)
    {
        using var response = await Api.ActingAs(TestUser.Joel).GetAsync($"api/sprints/{sprintId}/demo");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<SprintDemoDto>(text, Wire)!;
    }

    private async Task<Guid> AddFileAsync(Guid itemId, string name, string contentType, byte[] bytes)
    {
        using var scope = Host.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().AddAsync(itemId, name, contentType, new MemoryStream(bytes))).Id;
    }

    private Task<Guid> AddTextAsync(Guid itemId, string name, string contentType, string text) => AddFileAsync(itemId, name, contentType, Encoding.UTF8.GetBytes(text));

    private static Task RequestAsync(NpgsqlConnection database, Guid sprintId, DateTimeOffset at, DateTimeOffset? expires = null)
        => database.ExecuteAsync(
            "UPDATE sprint SET demo_requested_at = @at, demo_requested_by = @by, demo_requested_via = 'button', demo_upload_expires_at = @expires WHERE id = @sprintId",
            new { sprintId, at, by = SeedIds.Users.Joel, expires = expires ?? at.AddHours(24) });

    private static Task LiveSessionAsync(NpgsqlConnection database, Arranged demo, string state = "working", DateTimeOffset? at = null)
        => database.ExecuteAsync(
            """
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at, ended_at)
            VALUES (@id, @itemId, @connectionId, @state, 'https://github.com/ShipLean/ShipLean.git', 'develop', @at, @by, @at, CASE WHEN @state IN ('completed', 'failed', 'canceled') THEN @at END)
            """,
            new { id = Guid.CreateVersion7(), itemId = demo.ItemId, connectionId = demo.Project.ConnectionId, state, at = at ?? TimeProvider.System.GetUtcNow(), by = SeedIds.Users.Joel });

    private static string ClockOf(DateTimeOffset at) => DisplayDate.Clock(at, TimeProvider.System);

    private async Task<Delivered> DeliverAsync(NpgsqlConnection database, Arranged demo, string chaptersJson, string script, DateTimeOffset delivered, int videoBytes = 4321)
    {
        var video = await AddFileAsync(demo.ItemId, "sprint-demo.mp4", "video/mp4", new byte[videoBytes]);
        var chapters = await AddTextAsync(demo.ItemId, "sprint-demo-chapters.json", "application/json", chaptersJson);
        var scriptId = await AddTextAsync(demo.ItemId, "sprint-demo-script.md", "text/markdown", script);
        await database.ExecuteAsync(
            """
            UPDATE sprint SET demo_video_attachment_id = @video, demo_chapters_attachment_id = @chapters, demo_script_attachment_id = @scriptId,
                              demo_duration_seconds = 3723.4, demo_delivered_at = @delivered WHERE id = @sprintId
            """,
            new { video, chapters, scriptId, delivered, sprintId = demo.SprintId });
        return new Delivered(video, chapters, scriptId);
    }

    private static string ChaptersJson(params (string Title, decimal Start, string? Feature)[] chapters)
        => JsonSerializer.Serialize(new DemoChaptersJson(3723.4m, [.. chapters.Select(c => new DemoChapterJson(c.Title, c.Start, c.Feature))]), Wire);

    [Test]
    public async Task Get_WhenNoRow_ShouldBeNoneOrClosable()
    {
        await using var database = await OpenDatabaseAsync();
        var closed = await ArrangeAsync(database, withItem: false);
        var open = await ArrangeAsync(database, closed: false, withItem: false);

        var none = await GetAsync(closed.SprintId);
        var closable = await GetAsync(open.SprintId);

        none.Status.ShouldBe(SprintDemoStatus.None);
        none.Run.ShouldBeNull();
        none.Video.ShouldBeNull();
        none.Chapters.ShouldBeEmpty();
        none.Features.ShouldBeEmpty();
        none.ScriptHtml.ShouldBeNull();
        none.Actions.CanCreate.ShouldBeTrue();
        none.Actions.CreateBlockedMessage.ShouldBeNull();
        closable.Status.ShouldBe(SprintDemoStatus.Closable);
        closable.StatusMessage.ShouldBe("Close the sprint to make its demo.");
        closable.Actions.CanCreate.ShouldBeFalse();
        closable.Actions.CreateBlockedMessage.ShouldBe("Close the sprint to make its demo.");
    }

    [Test]
    public async Task Get_WhenWorking_ShouldLinkTheItemAndSessionState()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        var requested = TimeProvider.System.GetUtcNow().AddMinutes(-5);
        await RequestAsync(database, demo.SprintId, requested);
        await LiveSessionAsync(database, demo, "inputRequired");

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Working);
        dto.Run.ShouldNotBeNull();
        dto.Run.ItemKey.ShouldBe(demo.ItemKey);
        dto.Run.ItemUrl.ShouldBe($"/p/{demo.Project.Project.Key}/items/{demo.ItemKey}");
        dto.Run.SessionState.ShouldBe("inputRequired");
        dto.Run.RequestedLabel.ShouldBe($"Requested today at {ClockOf(requested)}");
        dto.Run.UploadExpiresLabel.ShouldNotBeNull().ShouldStartWith("Link valid until ");
        dto.Actions.CanCreate.ShouldBeFalse();
        dto.Actions.CreateBlockedMessage.ShouldBe("An agent session is already making this demo.");
    }

    [Test]
    public async Task Get_WhenReady_ShouldReturnVideoChaptersFeaturesScript()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        var api = Api.ActingAs(TestUser.Joel);
        var checkout = await demo.Project.CreateItemAsync(api, type: "feature");
        var coupons = await demo.Project.CreateItemAsync(api, demo.Project.Project.State(WorkItemTypeId.Feature, "Active"), "feature");
        var requested = TimeProvider.System.GetUtcNow().AddMinutes(-30);
        var delivered = TimeProvider.System.GetUtcNow().AddMinutes(-10);
        await RequestAsync(database, demo.SprintId, requested);
        var (videoId, _, _) = await DeliverAsync(
            database, demo,
            ChaptersJson(("Intro", 0, null), ("Checkout", 65, checkout.Key), ("Coupons", 3723.9m, coupons.Key), ("Checkout again", 3800, checkout.Key)),
            "## Intro\n\nWelcome to **Sprint 23**.\n\n<script>alert(1)</script>\n",
            delivered);

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Ready);
        var video = dto.Video.ShouldNotBeNull();
        video.AttachmentId.ShouldBe(videoId);
        video.ContentUrl.ShouldBe($"/api/attachments/{videoId}/content");
        video.ContentUrl.ShouldNotContain("/items/");
        video.Name.ShouldBe("sprint-demo.mp4");
        video.SizeBytes.ShouldBe(4321);
        video.DurationSeconds.ShouldBe(3723.4m);
        video.DurationLabel.ShouldBe("1:02:03");
        video.DeliveredLabel.ShouldBe($"Delivered today at {ClockOf(delivered)}");
        dto.Chapters.Select(c => (c.Title, c.StartLabel, c.FeatureKey)).ToList().ShouldBe(
            [("Intro", "0:00", null), ("Checkout", "1:05", checkout.Key), ("Coupons", "1:02:03", coupons.Key), ("Checkout again", "1:03:20", checkout.Key)]);
        dto.Chapters[1].StartSeconds.ShouldBe(65m);
        dto.Features.Select(f => f.Key).ToList().ShouldBe([checkout.Key, coupons.Key]);
        dto.Features[0].Title.ShouldBe("Add the thing");
        dto.Features[0].Url.ShouldBe($"/p/{demo.Project.Project.Key}/items/{checkout.Key}");
        dto.Features[0].State.Id.ShouldBe(demo.Project.Project.State(WorkItemTypeId.Feature, "New"));
        dto.Features[1].State.Id.ShouldBe(demo.Project.Project.State(WorkItemTypeId.Feature, "Active"));
        dto.ScriptHtml.ShouldNotBeNull().ShouldContain("Intro</h2>");
        dto.ScriptHtml.ShouldContain("<strong>Sprint 23</strong>");
        dto.ScriptHtml.ShouldNotContain("<script");
        dto.Actions.CanRetryYouTube.ShouldBeFalse();
    }

    [Test]
    public async Task Get_WhenRetryRunning_ShouldStillReturnTheOldVideo()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        await RequestAsync(database, demo.SprintId, TimeProvider.System.GetUtcNow().AddHours(-2));
        var (videoId, _, _) = await DeliverAsync(database, demo, ChaptersJson(("Intro", 0, null)), "Hello", TimeProvider.System.GetUtcNow().AddHours(-1));
        await RequestAsync(database, demo.SprintId, TimeProvider.System.GetUtcNow().AddMinutes(-1));
        await LiveSessionAsync(database, demo, "submitted");

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Working);
        dto.Video.ShouldNotBeNull().AttachmentId.ShouldBe(videoId);
        dto.Chapters.Select(c => c.Title).ToList().ShouldBe(["Intro"]);
        dto.ScriptHtml.ShouldNotBeNull().ShouldContain("Hello");
    }

    [Test]
    public async Task Get_WhenRetryFailed_ShouldBeFailedAndKeepTheOldVideo()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        await RequestAsync(database, demo.SprintId, TimeProvider.System.GetUtcNow().AddHours(-2));
        var (videoId, _, _) = await DeliverAsync(database, demo, ChaptersJson(("Intro", 0, null)), "Hello", TimeProvider.System.GetUtcNow().AddHours(-1));
        var retried = TimeProvider.System.GetUtcNow().AddMinutes(-10);
        await RequestAsync(database, demo.SprintId, retried);
        await LiveSessionAsync(database, demo, "canceled", TimeProvider.System.GetUtcNow().AddMinutes(-5));

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Failed);
        dto.StatusMessage.ShouldBe("The agent session ended as canceled before a video came back.");
        dto.Video.ShouldNotBeNull().AttachmentId.ShouldBe(videoId);
        dto.Run.ShouldNotBeNull().SessionState.ShouldBe("ended");
    }

    [Test]
    public async Task Get_WhenLinkExpiredAndNothingDelivered_ShouldBeFailedWithTheExpiryMessage()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        await RequestAsync(database, demo.SprintId, TimeProvider.System.GetUtcNow().AddHours(-30), TimeProvider.System.GetUtcNow().AddHours(-6));

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Failed);
        dto.StatusMessage.ShouldBe("The upload link expired before a video came back.");
        dto.Run.ShouldNotBeNull().UploadExpiresLabel.ShouldNotBeNull().ShouldStartWith("Link expired ");
    }

    [Test]
    public async Task Get_WhenAgentBlocked_ShouldDisableCreateWithReason()
    {
        await using var database = await OpenDatabaseAsync();
        var noConnection = await ArrangeAsync(database, agent: false, withItem: false);
        var noRepository = await ArrangeAsync(database, withItem: false);
        await database.ExecuteAsync("UPDATE project SET repo_url = NULL WHERE id = @id", new { id = noRepository.Project.Project.Id });

        var first = await GetAsync(noConnection.SprintId);
        var second = await GetAsync(noRepository.SprintId);

        first.Status.ShouldBe(SprintDemoStatus.None);
        first.Actions.CanCreate.ShouldBeFalse();
        first.Actions.CreateBlockedMessage.ShouldBe("This project has no agent connection. Choose one in the project's General settings.");
        second.Actions.CanCreate.ShouldBeFalse();
        second.Actions.CreateBlockedMessage.ShouldBe("This project has no repository. Add its URL in the project's General settings.");
    }

    [Test]
    public async Task Get_WhenPublishedUnlisted_ShouldHaveEmbed()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        await database.ExecuteAsync(
            "UPDATE sprint SET demo_youtube_state = 'published', demo_youtube_video_id = 'abc123XYZ_-', demo_youtube_privacy = 'unlisted' WHERE id = @sprintId", new { sprintId = demo.SprintId });

        var youTube = (await GetAsync(demo.SprintId)).YouTube;

        youTube.State.ShouldBe(YouTubeDemoState.Published);
        youTube.Privacy.ShouldBe(YouTubeVisibility.Unlisted);
        youTube.WatchUrl.ShouldBe("https://www.youtube.com/watch?v=abc123XYZ_-");
        youTube.EmbedUrl.ShouldBe("https://www.youtube-nocookie.com/embed/abc123XYZ_-");
        youTube.FailureMessage.ShouldBeNull();
    }

    [Test]
    public async Task Get_WhenPublishedPrivate_ShouldHaveNoEmbedButAWatchLink()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        await database.ExecuteAsync(
            "UPDATE sprint SET demo_youtube_state = 'published', demo_youtube_video_id = 'abc123XYZ_-', demo_youtube_privacy = 'private' WHERE id = @sprintId", new { sprintId = demo.SprintId });

        var youTube = (await GetAsync(demo.SprintId)).YouTube;

        youTube.Privacy.ShouldBe(YouTubeVisibility.Private);
        youTube.EmbedUrl.ShouldBeNull();
        youTube.WatchUrl.ShouldBe("https://www.youtube.com/watch?v=abc123XYZ_-");
    }

    [Test]
    public async Task Get_WhenYouTubeFailed_ShouldSayWhyAndKeepVideo()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        await RequestAsync(database, demo.SprintId, TimeProvider.System.GetUtcNow().AddHours(-2));
        var (videoId, _, _) = await DeliverAsync(database, demo, ChaptersJson(("Intro", 0, null)), "Hello", TimeProvider.System.GetUtcNow().AddHours(-1));
        await database.ExecuteAsync(
            "UPDATE sprint SET demo_youtube_state = 'failed', demo_youtube_failure = 'YouTube refused the upload: the daily quota is used up.' WHERE id = @sprintId", new { sprintId = demo.SprintId });

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Ready);
        dto.YouTube.State.ShouldBe(YouTubeDemoState.Failed);
        dto.YouTube.FailureMessage.ShouldBe("YouTube refused the upload: the daily quota is used up.");
        dto.YouTube.WatchUrl.ShouldBeNull();
        dto.YouTube.EmbedUrl.ShouldBeNull();
        dto.Video.ShouldNotBeNull().AttachmentId.ShouldBe(videoId);
        dto.Actions.CanRetryYouTube.ShouldBeTrue();
    }

    [Test]
    public async Task Get_WhenNotMember_ShouldBeNotFound()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);

        using var response = await Api.ActingAs(TestUser.Mei).GetAsync($"api/sprints/{demo.SprintId}/demo");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Get_WhenChaptersFileArchived_ShouldHaveNoChaptersAndNoError()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        var feature = await demo.Project.CreateItemAsync(Api.ActingAs(TestUser.Joel), type: "feature");
        await RequestAsync(database, demo.SprintId, TimeProvider.System.GetUtcNow().AddHours(-2));
        var (videoId, chaptersId, scriptId) = await DeliverAsync(database, demo, ChaptersJson(("Checkout", 0, feature.Key)), "Hello", TimeProvider.System.GetUtcNow().AddHours(-1));
        await database.ExecuteAsync("UPDATE attachment SET archived_at = now() WHERE id = @chaptersId", new { chaptersId });

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Ready);
        dto.Chapters.ShouldBeEmpty();
        dto.Features.ShouldBeEmpty();
        dto.Video.ShouldNotBeNull().AttachmentId.ShouldBe(videoId);
        dto.ScriptHtml.ShouldNotBeNull().ShouldContain("Hello");
        scriptId.ShouldNotBe(Guid.Empty);
    }

    [Test]
    public async Task Get_WhenScriptAndVideoFilesArchived_ShouldHaveNoScriptAndNoVideo()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        await RequestAsync(database, demo.SprintId, TimeProvider.System.GetUtcNow().AddHours(-2));
        var (videoId, _, scriptId) = await DeliverAsync(database, demo, ChaptersJson(("Intro", 0, null)), "Hello", TimeProvider.System.GetUtcNow().AddHours(-1));
        await database.ExecuteAsync("UPDATE attachment SET archived_at = now() WHERE id IN (@videoId, @scriptId)", new { videoId, scriptId });

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Ready);
        dto.Video.ShouldBeNull();
        dto.ScriptHtml.ShouldBeNull();
        dto.Chapters.Select(c => c.Title).ToList().ShouldBe(["Intro"]);
    }

    [Test]
    public async Task Get_WhenPointerNamesAnotherItemsFile_ShouldIgnoreIt()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        var other = await demo.Project.CreateItemAsync(Api.ActingAs(TestUser.Joel));
        var foreign = await AddFileAsync(other.Id, "secret.mp4", "video/mp4", new byte[77]);
        await database.ExecuteAsync("UPDATE sprint SET demo_video_attachment_id = @foreign WHERE id = @sprintId", new { foreign, sprintId = demo.SprintId });

        var dto = await GetAsync(demo.SprintId);

        dto.Video.ShouldBeNull();
    }

    [Test]
    public async Task Get_WhenChaptersFileIsNotJson_ShouldHaveNoChaptersAndNoError()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        await RequestAsync(database, demo.SprintId, TimeProvider.System.GetUtcNow().AddHours(-2));
        await DeliverAsync(database, demo, "this is not json {", "Hello", TimeProvider.System.GetUtcNow().AddHours(-1));

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Ready);
        dto.Chapters.ShouldBeEmpty();
        dto.Video.ShouldNotBeNull();
    }

    [Test]
    public async Task Get_WhenAChaptersFeatureWasRemoved_ShouldKeepTheChapterAndLeaveTheFeatureOut()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database);
        var api = Api.ActingAs(TestUser.Joel);
        var live = await demo.Project.CreateItemAsync(api, type: "feature");
        var removed = await demo.Project.CreateItemAsync(api, type: "feature");
        await database.ExecuteAsync("UPDATE work_item SET state_id = @state WHERE id = @id", new { removed.Id, state = demo.Project.Project.State(WorkItemTypeId.Feature, "Removed") });
        await RequestAsync(database, demo.SprintId, TimeProvider.System.GetUtcNow().AddHours(-2));
        await DeliverAsync(database, demo, ChaptersJson(("Gone", 0, removed.Key), ("Here", 10, live.Key)), "Hello", TimeProvider.System.GetUtcNow().AddHours(-1));

        var dto = await GetAsync(demo.SprintId);

        dto.Chapters.Select(c => c.FeatureKey).ToList().ShouldBe([removed.Key, live.Key]);
        dto.Features.Select(f => f.Key).ToList().ShouldBe([live.Key]);
    }

    [Test]
    public async Task Get_WhenBlockedWithNoItem_ShouldHaveNoRun()
    {
        await using var database = await OpenDatabaseAsync();
        var demo = await ArrangeAsync(database, withItem: false);
        await database.ExecuteAsync(
            "UPDATE sprint SET demo_requested_at = now(), demo_requested_by = @by, demo_requested_via = 'close', demo_blocked_message = 'No agent connection for this project.' WHERE id = @sprintId",
            new { by = SeedIds.Users.Joel, sprintId = demo.SprintId });

        var dto = await GetAsync(demo.SprintId);

        dto.Status.ShouldBe(SprintDemoStatus.Blocked);
        dto.StatusMessage.ShouldBe("No agent connection for this project.");
        dto.Run.ShouldBeNull();
        dto.Video.ShouldBeNull();
    }
}
