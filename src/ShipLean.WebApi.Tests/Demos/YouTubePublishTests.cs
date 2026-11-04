using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Fakes.YouTube;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Application.Features.Demos.YouTube;
using ShipLean.Application.Features.Instance.Integrations;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.Demos;

// K12's forward over the real database, attachment store, Data Protection and route, with the fake client behind IYouTubeClient. PublishDueAsync is
// called directly. It shows what the publisher does when YouTube answers as the fake is told to; it does not show that Google answers so.
[TestFixture]
public class YouTubePublishTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private IYouTubeClient _client = new FakeYouTubeClient();
    private readonly FixedYouTubeSettings _settings = new();
    private readonly RecordedLogs _logs = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IYouTubeClient>();
        services.AddScoped<IYouTubeClient>(_ => _client);
        services.RemoveAll<IYouTubeSettings>();
        services.AddSingleton<IYouTubeSettings>(_settings);
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(_logs);
    };

    private FakeYouTubeClient Fake => _client as FakeYouTubeClient ?? ((GatedYouTubeClient)_client).Inner;

    private sealed record Arranged(Guid ProjectId, string ProjectName, Guid SprintId, Guid ItemId, Guid VideoId, Guid ChaptersId, byte[] Video);

    private sealed record Stored(string State, string? VideoId, string? Privacy, string? Failure, DateTimeOffset? AttemptedAt);

    [SetUp]
    public async Task Reset()
    {
        _client = new FakeYouTubeClient();
        _settings.Client = new YouTubeClientSettings("client-id", "client-secret");
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE sprint SET demo_youtube_state = 'none' WHERE demo_youtube_state = 'pending'");
    }

    private DemoYouTubePublisher Publisher() => ActivatorUtilities.CreateInstance<DemoYouTubePublisher>(Host.Services);

    private static string Chapters(params (string Title, decimal Start)[] chapters)
        => JsonSerializer.Serialize(new DemoChaptersJson(4000m, [.. chapters.Select(c => new DemoChapterJson(c.Title, c.Start, null))]), Wire);

    private async Task<Guid> AddFileAsync(Guid itemId, string name, string contentType, byte[] bytes)
    {
        using var scope = Host.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().AddAsync(itemId, name, contentType, new MemoryStream(bytes))).Id;
    }

    // A project with a connected channel (visibility given; none for no channel row), a demo item holding the three files, and the sprint pending a forward.
    private async Task<Arranged> ArrangeAsync(
        string? channelVisibility = "unlisted", string state = "pending", string? chapters = null, string? failure = null, bool disconnected = false)
    {
        await using var database = await OpenDatabaseAsync();
        var project = await AgentDispatchArrange.CreateAsync(database, Api.ActingAs(TestUser.Joel), connection: false, repository: false);
        var sprintId = await DemoRunArrange.NewSprintAsync(database, project.Project.Id);
        using var scope = Host.Services.CreateScope();
        var itemId = await scope.ServiceProvider.GetRequiredService<IDemoRunStarter>().GetOrCreateItemAsync(project.Project.Id, sprintId, "Sprint 23", CancellationToken.None);

        var video = new byte[4096];
        Random.Shared.NextBytes(video);
        var videoId = await AddFileAsync(itemId, "sprint-demo.mp4", "video/mp4", video);
        var chaptersId = await AddFileAsync(itemId, "sprint-demo-chapters.json", "application/json", Encoding.UTF8.GetBytes(chapters ?? Chapters(("Intro", 0m), ("Search", 75.9m))));
        await database.ExecuteAsync(
            """
            UPDATE sprint SET demo_video_attachment_id = @videoId, demo_chapters_attachment_id = @chaptersId, demo_duration_seconds = 4000, demo_delivered_at = now(),
                              demo_youtube_state = @state, demo_youtube_failure = @failure WHERE id = @sprintId
            """,
            new { videoId, chaptersId, state, failure, sprintId });

        if (channelVisibility is not null)
        {
            // The refresh token is one the fake issued, stored as the connect step stores it, so the publisher has to open it.
            var issued = await Fake.ExchangeCodeAsync(_settings.Client!, "code", "https://x.test/cb", CancellationToken.None);
            var stored = disconnected ? string.Empty : Host.Services.GetRequiredService<IntegrationTokens>().Protect(issued.RefreshToken);
            await database.ExecuteAsync(
                """
                INSERT INTO project_youtube_channel (id, project_id, status, channel_id, channel_title, visibility, refresh_token, connected_by, archived_at)
                VALUES (@id, @projectId, 'connected', 'UC-fake-channel', 'Fake channel', @channelVisibility, @stored, @user, CASE WHEN @disconnected THEN now() END)
                """,
                new { id = Guid.NewGuid(), projectId = project.Project.Id, channelVisibility, stored, user = SeedIds.Users.Joel, disconnected });
        }

        var name = await database.ExecuteScalarAsync<string>("SELECT name FROM project WHERE id = @id", new { id = project.Project.Id });
        return new Arranged(project.Project.Id, name!, sprintId, itemId, videoId, chaptersId, video);
    }

    private async Task<Stored> StoredAsync(Guid sprintId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<Stored>(
            """
            SELECT demo_youtube_state AS State, demo_youtube_video_id AS VideoId, demo_youtube_privacy AS Privacy, demo_youtube_failure AS Failure,
                   demo_youtube_attempted_at AS AttemptedAt FROM sprint WHERE id = @sprintId
            """,
            new { sprintId });
    }

    private async Task<bool> LiveAsync(Guid attachmentId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<bool>("SELECT archived_at IS NULL FROM attachment WHERE id = @attachmentId", new { attachmentId });
    }

    private async Task<SprintDemoPointers> PointersAsync(Guid sprintId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<SprintDemoPointers>(
            "SELECT demo_video_attachment_id AS VideoPointer, demo_chapters_attachment_id AS ChaptersPointer, demo_work_item_id AS ItemPointer FROM sprint WHERE id = @sprintId", new { sprintId });
    }

    private sealed record SprintDemoPointers(Guid? VideoPointer, Guid? ChaptersPointer, Guid? ItemPointer);

    [Test]
    public async Task Publish_WhenPending_ShouldUploadKeptFileAtChannelVisibilityAndRecordIt()
    {
        var demo = await ArrangeAsync("unlisted");

        var published = await Publisher().PublishDueAsync();

        published.ShouldBeGreaterThanOrEqualTo(1);
        var sent = Fake.Uploads.Single();
        sent.Upload.Visibility.ShouldBe(YouTubeVisibility.Unlisted);
        sent.Upload.Title.ShouldBe($"{demo.ProjectName}: Sprint 23 demo");
        sent.Upload.ContentType.ShouldBe("video/mp4");
        sent.Content.ToArray().ShouldBe(demo.Video);
        var row = await StoredAsync(demo.SprintId);
        row.State.ShouldBe("published");
        row.VideoId.ShouldBe(sent.VideoId);
        row.Privacy.ShouldBe("unlisted");
        row.Failure.ShouldBeNull();
        row.AttemptedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task Publish_ShouldPutChaptersInTheDescription()
    {
        var demo = await ArrangeAsync(chapters: Chapters(("Intro", 0m), ("Search <fast>", 75.9m), ("Wrap up", 3725m)));

        await Publisher().PublishDueAsync();

        Fake.Uploads.Single().Upload.Description.ShouldBe("0:00 Intro\n1:15 Search fast\n1:02:05 Wrap up");
        (await StoredAsync(demo.SprintId)).State.ShouldBe("published");
    }

    [Test]
    public async Task Publish_WhenChaptersFileMissing_ShouldStillUploadWithABareDescription()
    {
        var demo = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE attachment SET archived_at = now() WHERE id = @id", new { id = demo.ChaptersId });
        }

        await Publisher().PublishDueAsync();

        Fake.Uploads.Single().Upload.Description.ShouldBe(string.Empty);
        (await StoredAsync(demo.SprintId)).State.ShouldBe("published");
    }

    [Test]
    public async Task Publish_WhenQuota_ShouldBeFailedSayingWhyAndKeepAttachment()
    {
        var demo = await ArrangeAsync();
        var before = await PointersAsync(demo.SprintId);
        Fake.FailWith(YouTubeFailureKind.Quota);

        await Publisher().PublishDueAsync();

        var row = await StoredAsync(demo.SprintId);
        row.State.ShouldBe("failed");
        row.Failure.ShouldBe("YouTube's daily upload quota for this project is used up. Try again tomorrow.");
        row.VideoId.ShouldBeNull();
        (await LiveAsync(demo.VideoId)).ShouldBeTrue();
        (await LiveAsync(demo.ChaptersId)).ShouldBeTrue();
        (await PointersAsync(demo.SprintId)).ShouldBe(before);

        // A failure waits for a person: with YouTube answering again, another pass does not try the sprint.
        Fake.FailWith(null);
        await Publisher().PublishDueAsync();
        Fake.Uploads.ShouldBeEmpty();
        (await StoredAsync(demo.SprintId)).State.ShouldBe("failed");
    }

    [Test]
    public async Task Publish_WhenRevoked_ShouldBeFailedSayingWhy()
    {
        var demo = await ArrangeAsync();
        Fake.FailWith(YouTubeFailureKind.Revoked);

        await Publisher().PublishDueAsync();

        var row = await StoredAsync(demo.SprintId);
        row.State.ShouldBe("failed");
        row.Failure.ShouldBe("Access to the YouTube channel was removed. Connect it again.");
        Fake.Uploads.ShouldBeEmpty();
    }

    [Test]
    public async Task Publish_WhenReadBackDiffers_ShouldRecordWhatYouTubeSaid()
    {
        var demo = await ArrangeAsync("unlisted");
        Fake.ForcedVisibility = YouTubeVisibility.Private;

        await Publisher().PublishDueAsync();

        Fake.Uploads.Single().Upload.Visibility.ShouldBe(YouTubeVisibility.Unlisted);
        var row = await StoredAsync(demo.SprintId);
        row.State.ShouldBe("published");
        row.Privacy.ShouldBe("private");
        row.VideoId.ShouldBe(Fake.Uploads.Single().VideoId);
    }

    [Test]
    public async Task Publish_WhenNoChannel_ShouldReturnToNone()
    {
        var none = await ArrangeAsync(channelVisibility: null);
        var disconnected = await ArrangeAsync("unlisted", disconnected: true);

        await Publisher().PublishDueAsync();

        Fake.Uploads.ShouldBeEmpty();
        (await StoredAsync(none.SprintId)).State.ShouldBe("none");
        (await StoredAsync(disconnected.SprintId)).State.ShouldBe("none");
    }

    [Test]
    [CancelAfter(90000)]
    public async Task Publish_WhenTwoRunners_ShouldUploadOnce()
    {
        var gate = new GatedYouTubeClient();
        _client = gate;
        var demo = await ArrangeAsync();

        var first = Publisher().PublishDueAsync();
        await gate.FirstUploadReached;

        // The first runner is mid-upload. A second one that took the same sprint would upload it again (the gate lets a second arrival straight through).
        var second = await Publisher().PublishDueAsync();
        gate.Open();
        var firstPublished = await first;

        second.ShouldBe(0);
        firstPublished.ShouldBe(1);
        gate.Arrivals.ShouldBe(1);
        gate.Inner.Uploads.Count.ShouldBe(1);
        (await StoredAsync(demo.SprintId)).State.ShouldBe("published");
    }

    [Test]
    public async Task Publish_WhenAttachmentArchived_ShouldBeFailedSayingWhy()
    {
        var demo = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE attachment SET archived_at = now() WHERE id = @id", new { id = demo.VideoId });
        }

        await Publisher().PublishDueAsync();

        var row = await StoredAsync(demo.SprintId);
        row.State.ShouldBe("failed");
        row.Failure.ShouldStartWith("The demo video is no longer on the work item");
        Fake.Uploads.ShouldBeEmpty();
        (await LiveAsync(demo.VideoId)).ShouldBeFalse();
        (await PointersAsync(demo.SprintId)).VideoPointer.ShouldBe(demo.VideoId);
    }

    [Test]
    public async Task Retry_WhenFailed_ShouldSetPending()
    {
        var demo = await ArrangeAsync(state: "failed", failure: "YouTube's daily upload quota for this project is used up. Try again tomorrow.");
        var events = new List<ProjectEvent>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(demo.ProjectId, e =>
        {
            lock (events)
            {
                events.Add(e);
            }

            return Task.CompletedTask;
        });

        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"api/sprints/{demo.SprintId}/demo/youtube/retry");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var dto = JsonSerializer.Deserialize<SprintDemoDto>(text, Wire)!;
        dto.YouTube.State.ShouldBe(YouTubeDemoState.Pending);
        dto.YouTube.FailureMessage.ShouldBeNull();
        var row = await StoredAsync(demo.SprintId);
        row.State.ShouldBe("pending");
        row.Failure.ShouldBeNull();
        row.AttemptedAt.ShouldBeNull();
        (await LiveAsync(demo.VideoId)).ShouldBeTrue();
        lock (events)
        {
            events.OfType<SprintDemoChanged>().ShouldContain(e => e.SprintId == demo.SprintId);
        }

        await Publisher().PublishDueAsync();
        (await StoredAsync(demo.SprintId)).State.ShouldBe("published");
    }

    [Test]
    public async Task Retry_WhenNotFailed_ShouldConflict()
    {
        var pending = await ArrangeAsync(state: "pending");
        var published = await ArrangeAsync(state: "published");

        using var onPending = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"api/sprints/{pending.SprintId}/demo/youtube/retry");
        using var onPublished = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"api/sprints/{published.SprintId}/demo/youtube/retry");

        onPending.StatusCode.ShouldBe(HttpStatusCode.Conflict, await onPending.Content.ReadAsStringAsync());
        onPublished.StatusCode.ShouldBe(HttpStatusCode.Conflict, await onPublished.Content.ReadAsStringAsync());
        (await StoredAsync(pending.SprintId)).State.ShouldBe("pending");
        (await StoredAsync(published.SprintId)).State.ShouldBe("published");
    }

    [Test]
    public async Task Retry_WhenNotMember_ShouldBeNotFound()
    {
        var demo = await ArrangeAsync(state: "failed", failure: "YouTube refused the request.");

        using var response = await Api.ActingAs(TestUser.Priya).PostJsonAsync($"api/sprints/{demo.SprintId}/demo/youtube/retry");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
        (await StoredAsync(demo.SprintId)).State.ShouldBe("failed");
    }

    [Test]
    public async Task Publish_ShouldPublishSprintDemoChanged()
    {
        var published = await ArrangeAsync();
        var failed = await ArrangeAsync(channelVisibility: "unlisted");
        var seen = new List<Guid>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(null, e =>
        {
            if (e is SprintDemoChanged changed)
            {
                lock (seen) { seen.Add(changed.SprintId); }
            }

            return Task.CompletedTask;
        });

        await Publisher().PublishDueAsync();

        lock (seen)
        {
            seen.ShouldContain(published.SprintId);
            seen.ShouldContain(failed.SprintId);
        }
    }

    [Test]
    public async Task Publish_ShouldLogNoToken()
    {
        var ok = await ArrangeAsync();
        var quota = await ArrangeAsync();
        await Publisher().PublishDueAsync();
        Fake.FailWith(YouTubeFailureKind.Quota);
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE sprint SET demo_youtube_state = 'pending', demo_youtube_attempted_at = NULL WHERE id = @id", new { id = ok.SprintId });
        }

        await Publisher().PublishDueAsync();

        var mine = _logs.Events.Where(e => e.Contains("DemoYouTubePublisher", StringComparison.Ordinal)).ToList();
        mine.ShouldContain(e => e.Contains(ok.SprintId.ToString(), StringComparison.OrdinalIgnoreCase));
        mine.ShouldContain(e => e.StartsWith("Warning", StringComparison.Ordinal));
        mine.ShouldNotContain(e => e.Contains("access-", StringComparison.Ordinal) || e.Contains("refresh-", StringComparison.Ordinal) || e.Contains("client-secret", StringComparison.Ordinal));
        _ = quota;
    }
}
