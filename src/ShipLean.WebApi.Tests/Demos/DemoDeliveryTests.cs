using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Storage;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Demos;

// K8's delivery over the real attachment store, blob store and database. A good set is delivered first in each failure test, so there is an old set to keep.
[TestFixture]
public class DemoDeliveryTests : ApiFixture
{
    private FailingBlobStore _blobs = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        var real = services.Single(d => d.ServiceType == typeof(IBlobStore));
        services.Remove(real);
        services.AddSingleton<IBlobStore>(sp => _blobs = new FailingBlobStore((IBlobStore)real.ImplementationFactory!(sp)));
    };

    [SetUp]
    public void HealBlobs() => _blobs?.Heal();

    private sealed record Arranged(Guid ProjectId, Guid SprintId, Guid ItemId);

    private sealed record Pointers(Guid? VideoId, Guid? ScriptId, Guid? ChaptersId, decimal? DurationSeconds, string YouTubeState);

    private async Task<Arranged> ArrangeAsync()
    {
        await using var database = await OpenDatabaseAsync();
        using var scope = Host.Services.CreateScope();
        var (projectId, sprintId, itemId) = await DemoRunArrange.NewDemoAsync(database, Api, scope.ServiceProvider.GetRequiredService<IDemoRunStarter>());
        _ = Host.Services.GetRequiredService<IBlobStore>();
        return new Arranged(projectId, sprintId, itemId);
    }

    private static DemoChaptersJson Chapters(decimal duration = 142.6m)
        => new(duration, [new DemoChapterJson("Intro", 0, null), new DemoChapterJson("Checkout", 15.2m, null)]);

    private async Task<string> TokenAsync(Arranged demo, bool claim = false)
    {
        using var scope = Host.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITransferTokens>();
        var issued = await tokens.IssueAsync(new NewTransfer(TransferKind.Demo, demo.ItemId, null, "sprint-demo.mp4", "video/mp4", SeedIds.Users.Joel, null, "Claude Code", TimeSpan.FromHours(24)));
        if (claim)
        {
            (await tokens.ClaimAsync(issued.Token)).ShouldBeTrue();
        }

        return issued.Token;
    }

    private async Task<DemoUploadedDto> DeliverAsync(
        Arranged demo, string script = "# Demo", int videoBytes = 300, byte fill = 1, AttachmentLimits? limits = null, string? token = null, DemoChaptersJson? chapters = null)
    {
        using var scope = Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        token ??= await TokenAsync(demo);
        var grant = await services.GetRequiredService<ITransferTokens>().FindAsync(TransferKind.Demo, token)
            ?? new TransferGrant(TransferKind.Demo, demo.ItemId, null, "sprint-demo.mp4", "video/mp4", SeedIds.Users.Joel, "Claude Code", new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var effective = limits ?? services.GetRequiredService<AttachmentLimits>();
        var store = new AttachmentStore(
            services.GetRequiredService<NpgsqlDataSource>(), services.GetRequiredService<ICurrentUser>(), services.GetRequiredService<IBlobStore>(),
            TimeProvider.System, services.GetRequiredService<IProjectEvents>(), effective);
        var delivery = new DemoDelivery(store, services.GetRequiredService<ITransferTokens>(), services.GetRequiredService<IProjectEvents>(), effective);
        return await delivery.DeliverAsync(
            new DemoDeliveryParts(grant, token, chapters ?? Chapters(), script, new MemoryStream(Enumerable.Repeat(fill, videoBytes).ToArray())), CancellationToken.None);
    }

    private async Task<Pointers> PointersAsync(Guid sprintId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<Pointers>(
            "SELECT demo_video_attachment_id AS VideoId, demo_script_attachment_id AS ScriptId, demo_chapters_attachment_id AS ChaptersId, demo_duration_seconds AS DurationSeconds, demo_youtube_state AS YouTubeState FROM sprint WHERE id = @sprintId",
            new { sprintId });
    }

    private async Task<IReadOnlyList<AttachmentDto>> LiveAsync(Guid itemId)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().ListAsync(itemId);
    }

    private async Task<byte[]> BytesAsync(Guid attachmentId)
    {
        using var scope = Host.Services.CreateScope();
        var (_, content) = await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().OpenAsync(attachmentId);
        await using var stream = content.Content;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }

    private async Task AssertOldSetLiveAsync(Arranged demo, DemoUploadedDto old, Pointers oldPointers, int expectedLive = 3)
    {
        (await PointersAsync(demo.SprintId)).ShouldBe(oldPointers);
        var live = await LiveAsync(demo.ItemId);
        live.Count.ShouldBe(expectedLive);
        live.Select(a => a.Id).ShouldContain(oldPointers.VideoId!.Value);
        live.Select(a => a.Id).ShouldContain(oldPointers.ScriptId!.Value);
        live.Select(a => a.Id).ShouldContain(oldPointers.ChaptersId!.Value);
        (await BytesAsync(old.VideoAttachmentId)).ShouldBe(Enumerable.Repeat((byte)1, 300).ToArray());
    }

    [Test]
    public async Task Deliver_WhenValid_ShouldStoreThreeFilesAndMoveThePointers()
    {
        var demo = await ArrangeAsync();

        var uploaded = await DeliverAsync(demo, "# The demo\n\nShip it.", videoBytes: 4096, fill: 7);

        var pointers = await PointersAsync(demo.SprintId);
        pointers.VideoId.ShouldBe(uploaded.VideoAttachmentId);
        pointers.DurationSeconds.ShouldBe(142.60m);
        var live = (await LiveAsync(demo.ItemId)).ToDictionary(a => a.Name);
        live.Keys.OrderBy(n => n).ShouldBe(["sprint-demo-chapters.json", "sprint-demo-script.md", "sprint-demo.mp4"]);
        live["sprint-demo.mp4"].Id.ShouldBe(pointers.VideoId!.Value);
        live["sprint-demo-script.md"].Id.ShouldBe(pointers.ScriptId!.Value);
        live["sprint-demo-chapters.json"].Id.ShouldBe(pointers.ChaptersId!.Value);
        live["sprint-demo.mp4"].ContentType.ShouldBe("video/mp4");
        (await BytesAsync(pointers.VideoId.Value)).ShouldBe(Enumerable.Repeat((byte)7, 4096).ToArray());
        Encoding.UTF8.GetString(await BytesAsync(pointers.ScriptId.Value)).ShouldBe("# The demo\n\nShip it.");
        Encoding.UTF8.GetString(await BytesAsync(pointers.ChaptersId.Value)).ShouldContain("\"Checkout\"");
        uploaded.VideoBytes.ShouldBe(4096);
        uploaded.Chapters.ShouldBe(2);
        uploaded.OpenUrl.ShouldEndWith($"/sprints/{demo.SprintId}/demo");
        await using var database = await OpenDatabaseAsync();
        (await database.ExecuteScalarAsync<DateTimeOffset?>("SELECT demo_delivered_at FROM sprint WHERE id = @id", new { id = demo.SprintId })).ShouldNotBeNull();
    }

    [Test]
    public async Task Deliver_WhenChaptersPartFails_ShouldLeaveOldFilesAndPointersLive()
    {
        (await FailPartAsync("application/json")).ShouldBe("The blob store failed on application/json.");
    }

    [Test]
    public async Task Deliver_WhenScriptPartFails_ShouldLeaveOldFilesAndPointersLive()
    {
        (await FailPartAsync("text/markdown")).ShouldBe("The blob store failed on text/markdown.");
    }

    [Test]
    public async Task Deliver_WhenVideoPartFails_ShouldLeaveOldFilesAndPointersLive()
    {
        (await FailPartAsync("video/mp4")).ShouldBe("The blob store failed on video/mp4.");
    }

    // The part is named by its content type (chapters application/json, script text/markdown, video video/mp4), so the order of the puts doesn't matter.
    private async Task<string> FailPartAsync(string contentType)
    {
        var demo = await ArrangeAsync();
        var old = await DeliverAsync(demo);
        var oldPointers = await PointersAsync(demo.SprintId);
        _blobs.FailOnContentType(contentType);

        var failure = await Should.ThrowAsync<IOException>(async () => await DeliverAsync(demo, "# Newer", videoBytes: 500, fill: 9));

        await AssertOldSetLiveAsync(demo, old, oldPointers);
        return failure.Message;
    }

    [Test]
    public async Task Deliver_WhenPointerWriteFails_ShouldLeaveOldFilesAndPointersLive()
    {
        var demo = await ArrangeAsync();
        var old = await DeliverAsync(demo);
        var oldPointers = await PointersAsync(demo.SprintId);
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE sprint SET demo_work_item_id = NULL WHERE id = @id", new { id = demo.SprintId });
        }

        await Should.ThrowAsync<InvalidOperationException>(async () => await DeliverAsync(demo, "# Newer", videoBytes: 500, fill: 9));

        await AssertOldSetLiveAsync(demo, old, oldPointers);
    }

    [Test]
    public async Task Deliver_WhenRevokedAfterClaim_ShouldRollBackAndLeaveTheNewRunsPointers()
    {
        var demo = await ArrangeAsync();
        var old = await DeliverAsync(demo);
        var oldPointers = await PointersAsync(demo.SprintId);
        var stale = await TokenAsync(demo, claim: true);
        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITransferTokens>().RevokeAsync(TransferKind.Demo, demo.ItemId);
        }

        var refused = await Should.ThrowAsync<ApiException>(async () => await DeliverAsync(demo, "# Stale", videoBytes: 500, fill: 9, token: stale));

        refused.Code.ShouldBe(ApiErrorCodes.NotFound);
        refused.Message.ShouldBe("That upload link is not valid.");
        await AssertOldSetLiveAsync(demo, old, oldPointers);
    }

    [Test]
    public async Task Deliver_WhenVideoOverDemoLimit_ShouldBeValidationAndChangeNothing()
    {
        var demo = await ArrangeAsync();
        var old = await DeliverAsync(demo);
        var oldPointers = await PointersAsync(demo.SprintId);

        var refused = await Should.ThrowAsync<ApiException>(async () => await DeliverAsync(demo, videoBytes: 2000, limits: new AttachmentLimits(10_000, 100_000, 1000)));

        refused.Code.ShouldBe(ApiErrorCodes.Validation);

        await AssertOldSetLiveAsync(demo, old, oldPointers);
    }

    [Test]
    public async Task Deliver_WhenVideoOverOldFileLimitButUnderDemoLimit_ShouldStore()
    {
        var demo = await ArrangeAsync();

        var uploaded = await DeliverAsync(demo, videoBytes: 3000, limits: new AttachmentLimits(1000, 100_000, 5000));

        uploaded.VideoBytes.ShouldBe(3000);
        (await PointersAsync(demo.SprintId)).VideoId.ShouldBe(uploaded.VideoAttachmentId);
    }

    [Test]
    public async Task Deliver_WhenRetried_ShouldReplaceAndCountOnlyTheNewFiles()
    {
        var demo = await ArrangeAsync();
        var limits = new AttachmentLimits(10_000, 7000, 5000);
        var first = await DeliverAsync(demo, videoBytes: 3500, limits: limits);

        var second = await DeliverAsync(demo, videoBytes: 3500, fill: 2, limits: limits);

        second.VideoAttachmentId.ShouldNotBe(first.VideoAttachmentId);
        var live = await LiveAsync(demo.ItemId);
        live.Count.ShouldBe(3);
        live.ShouldNotContain(a => a.Id == first.VideoAttachmentId);
        (await PointersAsync(demo.SprintId)).VideoId.ShouldBe(second.VideoAttachmentId);
        (await BytesAsync(second.VideoAttachmentId))[0].ShouldBe((byte)2);
    }

    [Test]
    public async Task Deliver_WhenOverTheItemTotal_ShouldBeValidationAndChangeNothing()
    {
        var demo = await ArrangeAsync();
        var old = await DeliverAsync(demo);
        var oldPointers = await PointersAsync(demo.SprintId);
        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().AddAsync(demo.ItemId, "other.bin", "application/octet-stream", new MemoryStream(new byte[5000]));
        }

        var refused = await Should.ThrowAsync<ApiException>(async () => await DeliverAsync(demo, videoBytes: 3500, fill: 9, limits: new AttachmentLimits(10_000, 7000, 5000)));

        refused.Code.ShouldBe(ApiErrorCodes.Validation);
        await AssertOldSetLiveAsync(demo, old, oldPointers, expectedLive: 4);
    }

    [Test]
    public async Task Deliver_WhenChannelConnected_ShouldSetYouTubePending()
    {
        var demo = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync(
                "INSERT INTO project_youtube_channel (id, project_id, status, channel_id, channel_title, refresh_token, connected_by) VALUES (@id, @projectId, 'connected', 'UC1', 'Channel', 'x', @user)",
                new { id = Guid.NewGuid(), projectId = demo.ProjectId, user = SeedIds.Users.Joel });
        }

        await DeliverAsync(demo);

        (await PointersAsync(demo.SprintId)).YouTubeState.ShouldBe("pending");
    }

    [Test]
    public async Task Deliver_WhenNoChannel_ShouldSetYouTubeNone()
    {
        var demo = await ArrangeAsync();

        await DeliverAsync(demo);

        (await PointersAsync(demo.SprintId)).YouTubeState.ShouldBe("none");
    }

    [Test]
    public async Task Deliver_ShouldClearTheOldYouTubeResult()
    {
        var demo = await ArrangeAsync();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            """
            UPDATE sprint SET demo_youtube_state = 'failed', demo_youtube_video_id = 'abc', demo_youtube_privacy = 'unlisted',
                demo_youtube_failure = 'Quota', demo_youtube_attempted_at = now() WHERE id = @id
            """, new { id = demo.SprintId });

        await DeliverAsync(demo);

        var row = await database.QuerySingleAsync<(string State, string? VideoId, string? Privacy, string? Failure, DateTimeOffset? Attempted)>(
            "SELECT demo_youtube_state, demo_youtube_video_id, demo_youtube_privacy, demo_youtube_failure, demo_youtube_attempted_at FROM sprint WHERE id = @id", new { id = demo.SprintId });
        row.ShouldBe(("none", null, null, null, null));
    }

    [Test]
    public async Task Deliver_ShouldPublishSprintDemoChangedAfterTheCommit()
    {
        var demo = await ArrangeAsync();
        var events = new RecordedDemoDeliveryEvents(Host.Services.GetRequiredService<NpgsqlDataSource>());
        using var scope = Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IAttachmentStore>();
        var token = await TokenAsync(demo);
        var grant = (await services.GetRequiredService<ITransferTokens>().FindAsync(TransferKind.Demo, token))!;

        var uploaded = await new DemoDelivery(store, services.GetRequiredService<ITransferTokens>(), events, services.GetRequiredService<AttachmentLimits>())
            .DeliverAsync(new DemoDeliveryParts(grant, token, Chapters(), "# Demo", new MemoryStream(new byte[100])), CancellationToken.None);

        events.Published.ShouldBe([new SprintDemoChanged(demo.ProjectId, demo.SprintId)]);
        events.VideoPointerAtPublish.ShouldBe(uploaded.VideoAttachmentId);
    }

    [Test]
    public async Task Deliver_ShouldStampTheAttachmentsWithTheAgent()
    {
        var demo = await ArrangeAsync();

        await DeliverAsync(demo);

        var live = await LiveAsync(demo.ItemId);
        live.Count.ShouldBe(3);
        live.ShouldAllBe(a => a.AgentName == "Claude Code" && a.Via == "create_sprint_demo");
    }
}
