using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Instance.Integrations;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Demos;

// K12's check over the real route, service, database and Data Protection, with the fake client behind IYouTubeClient. It shows what the check does when
// YouTube answers as the fake is told to; it does not show that Google answers so. Each test arranges a project of its own, Joel its admin and Priya a member.
[TestFixture]
public class YouTubeCheckTests : ApiFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private FaultingYouTubeClient _client = new();
    private readonly FixedYouTubeSettings _settings = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IYouTubeClient>();
        services.AddScoped<IYouTubeClient>(_ => _client);
        services.RemoveAll<IYouTubeSettings>();
        services.AddSingleton<IYouTubeSettings>(_settings);
    };

    [SetUp]
    public void Reset()
    {
        _client = new FaultingYouTubeClient();
        _settings.Client = new YouTubeClientSettings("client-id", "client-secret");
    }

    private sealed record StoredChannel(string Status, string? Visibility, string? Refusal, DateTimeOffset? CheckedAt);

    private async Task<ArrangedProject> ArrangeAsync(string status = "pending", string? visibility = null, bool channel = true)
    {
        await using var database = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(database, (SeedIds.Users.Joel, ProjectRole.Admin), (SeedIds.Users.Priya, ProjectRole.Member));
        if (channel)
        {
            // The refresh token is one the fake issued, stored as the connect step stores it, so the check has to open it.
            var issued = await _client.Inner.ExchangeCodeAsync(_settings.Client!, "code", "https://x.test/cb", CancellationToken.None);
            var stored = Host.Services.GetRequiredService<IntegrationTokens>().Protect(issued.RefreshToken);
            await database.ExecuteAsync(
                """
                INSERT INTO project_youtube_channel (id, project_id, status, channel_id, channel_title, visibility, refresh_token, connected_by)
                VALUES (@id, @projectId, @status, 'UC-fake-channel', 'Fake channel', @visibility, @stored, @user)
                """,
                new { id = Guid.NewGuid(), projectId = project.Id, status, visibility, stored, user = SeedIds.Users.Joel });
        }

        return project;
    }

    private async Task<StoredChannel> StoredAsync(ArrangedProject project)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<StoredChannel>(
            "SELECT status, visibility, refusal, checked_at AS CheckedAt FROM project_youtube_channel WHERE project_id = @id AND archived_at IS NULL", new { id = project.Id });
    }

    private Task<HttpResponseMessage> CheckRawAsync(ArrangedProject project, string visibility, ApiClient? api = null)
        => (api ?? Api).PostJsonAsync($"api/projects/{project.Id}/youtube/check", new { visibility });

    private async Task<YouTubeProjectDto> CheckAsync(ArrangedProject project, string visibility)
    {
        using var response = await CheckRawAsync(project, visibility);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<YouTubeProjectDto>(text, WireOptions)!;
    }

    // The same call without the HTTP layer, for the case where the service itself throws.
    private async Task<YouTubeProjectDto> CheckDirectAsync(ArrangedProject project, YouTubeVisibility visibility)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IYouTubeConnections>().CheckAsync(project.Id, visibility);
    }

    private void ShouldHaveDeletedEveryUpload()
    {
        _client.Inner.Uploads.ShouldNotBeEmpty();
        _client.Inner.Deletes.ShouldBe(_client.Inner.Uploads.Select(u => u.VideoId));
    }

    [Test]
    public async Task Check_WhenMatches_ShouldConnectAndDeleteTheTestVideo()
    {
        var project = await ArrangeAsync();

        var result = await CheckAsync(project, "unlisted");

        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(YouTubeConnectionStatus.Connected),
            () => result.Visibility.ShouldBe(YouTubeVisibility.Unlisted),
            () => result.ChannelTitle.ShouldBe("Fake channel"),
            () => result.Refusal.ShouldBeNull());
        var upload = _client.Inner.Uploads.ShouldHaveSingleItem();
        upload.Upload.Visibility.ShouldBe(YouTubeVisibility.Unlisted);
        upload.Upload.ContentType.ShouldBe("video/mp4");
        upload.Content.Length.ShouldBeGreaterThan(1000);
        _client.Inner.Deletes.ShouldBe([upload.VideoId]);
        var stored = await StoredAsync(project);
        stored.Status.ShouldBe("connected");
        stored.Visibility.ShouldBe("unlisted");
        stored.Refusal.ShouldBeNull();
        stored.CheckedAt.ShouldNotBeNull();
        result.CheckedLabel.ShouldBe($"Checked today at {DisplayDate.Clock(stored.CheckedAt.Value, TimeProvider.System)}");
    }

    [Test]
    public async Task Check_WhenPrivateCameBackAndUnlistedChosen_ShouldRefuseStayPendingAndNameBoth()
    {
        var project = await ArrangeAsync();
        _client.Inner.ForcedVisibility = YouTubeVisibility.Private;

        var result = await CheckAsync(project, "unlisted");

        result.Status.ShouldBe(YouTubeConnectionStatus.Pending);
        result.Visibility.ShouldBeNull();
        result.CheckedLabel.ShouldBeNull();
        result.Refusal.ShouldNotBeNull().ShouldContain("YouTube gave the test video private visibility, not the unlisted you chose.");
        var stored = await StoredAsync(project);
        stored.Status.ShouldBe("pending");
        stored.Visibility.ShouldBeNull();
        stored.Refusal.ShouldBe(result.Refusal);
        ShouldHaveDeletedEveryUpload();
    }

    [Test]
    public async Task Check_WhenPrivateCameBackAndPublicChosen_ShouldRefuse()
    {
        var project = await ArrangeAsync();
        _client.Inner.ForcedVisibility = YouTubeVisibility.Private;

        var result = await CheckAsync(project, "public");

        result.Status.ShouldBe(YouTubeConnectionStatus.Pending);
        result.Refusal.ShouldNotBeNull().ShouldContain("private visibility, not the public you chose.");
        (await StoredAsync(project)).Status.ShouldBe("pending");
        ShouldHaveDeletedEveryUpload();
    }

    [Test]
    public async Task Check_WhenPublicCameBackAndPrivateChosen_ShouldRefuse()
    {
        var project = await ArrangeAsync();
        _client.Inner.ForcedVisibility = YouTubeVisibility.Public;

        var result = await CheckAsync(project, "private");

        result.Status.ShouldBe(YouTubeConnectionStatus.Pending);
        result.Refusal.ShouldNotBeNull().ShouldContain("public visibility, not the private you chose.");
        (await StoredAsync(project)).Status.ShouldBe("pending");
        ShouldHaveDeletedEveryUpload();
    }

    [Test]
    public async Task Check_AfterRefusalWhenTheVisibilityThatCameBackIsChosen_ShouldConnect()
    {
        var project = await ArrangeAsync();
        _client.Inner.ForcedVisibility = YouTubeVisibility.Private;
        (await CheckAsync(project, "unlisted")).Status.ShouldBe(YouTubeConnectionStatus.Pending);

        var result = await CheckAsync(project, "private");

        result.Status.ShouldBe(YouTubeConnectionStatus.Connected);
        result.Visibility.ShouldBe(YouTubeVisibility.Private);
        result.Refusal.ShouldBeNull();
        var stored = await StoredAsync(project);
        stored.Status.ShouldBe("connected");
        stored.Visibility.ShouldBe("private");
        stored.Refusal.ShouldBeNull();
        _client.Inner.Uploads.Count.ShouldBe(2);
        ShouldHaveDeletedEveryUpload();
    }

    [Test]
    public async Task Check_WhenRefused_ShouldSayTheChannelOrAccountSettingsDecideIt()
    {
        var project = await ArrangeAsync();
        _client.Inner.ForcedVisibility = YouTubeVisibility.Private;

        var result = await CheckAsync(project, "unlisted");

        result.Refusal.ShouldBe(
            "YouTube gave the test video private visibility, not the unlisted you chose. The channel or its account settings decide this. "
            + "Change the channel's setting and check again, choose private, or don't use YouTube.");
    }

    [Test]
    public async Task Check_WhenReadBackThrows_ShouldStillDelete()
    {
        var failed = await ArrangeAsync();
        _client.ReadBackThrows = new YouTubeFailureException(YouTubeFailureKind.Network, "YouTube could not be reached.");

        var told = await CheckAsync(failed, "unlisted");

        told.Status.ShouldBe(YouTubeConnectionStatus.Pending);
        told.Refusal.ShouldBe("YouTube could not be reached.");
        (await StoredAsync(failed)).Status.ShouldBe("pending");
        ShouldHaveDeletedEveryUpload();

        // An exception nobody planned for still leaves no video behind, and still connects nothing.
        _client = new FaultingYouTubeClient { ReadBackThrows = new InvalidOperationException("read-back blew up") };
        var unplanned = await ArrangeAsync();

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => CheckDirectAsync(unplanned, YouTubeVisibility.Unlisted));

        thrown.Message.ShouldBe("read-back blew up");
        (await StoredAsync(unplanned)).Status.ShouldBe("pending");
        ShouldHaveDeletedEveryUpload();
    }

    [Test]
    public async Task Check_WhenDeleteFails_ShouldSayWhichVideo()
    {
        var project = await ArrangeAsync();
        _client.DeleteThrows = new YouTubeFailureException(YouTubeFailureKind.Network, "YouTube could not be reached.");

        var result = await CheckAsync(project, "unlisted");

        var left = _client.Inner.Uploads.ShouldHaveSingleItem().VideoId;
        result.Status.ShouldBe(YouTubeConnectionStatus.Pending);
        result.Refusal.ShouldNotBeNull().ShouldContain($"The test video {left} could not be deleted");
        result.Refusal.ShouldContain("YouTube Studio");
        var stored = await StoredAsync(project);
        stored.Status.ShouldBe("pending");
        stored.Refusal.ShouldBe(result.Refusal);
    }

    [Test]
    public async Task Check_WhenQuota_ShouldStayPendingAndSayWhy()
    {
        var project = await ArrangeAsync();
        _client.Inner.FailWith(YouTubeFailureKind.Quota);

        var result = await CheckAsync(project, "unlisted");

        result.Status.ShouldBe(YouTubeConnectionStatus.Pending);
        result.Refusal.ShouldBe("YouTube's daily upload quota for this project is used up. Try again tomorrow.");
        var stored = await StoredAsync(project);
        stored.Status.ShouldBe("pending");
        stored.Refusal.ShouldBe(result.Refusal);
        _client.Inner.Uploads.ShouldBeEmpty();
    }

    [Test]
    public async Task Check_WhenNoChannel_ShouldConflict()
    {
        var project = await ArrangeAsync(channel: false);

        using var response = await CheckRawAsync(project, "unlisted");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        _client.Inner.Uploads.ShouldBeEmpty();
    }

    [Test]
    public async Task Check_WhenYouTubeIsOff_ShouldBeNotFound()
    {
        var project = await ArrangeAsync();
        _settings.Client = null;

        using var response = await CheckRawAsync(project, "unlisted");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        _client.Inner.Uploads.ShouldBeEmpty();
    }

    [Test]
    public async Task Check_WhenChangingVisibility_ShouldRunAgainAndNotConnectUntilItPasses()
    {
        var project = await ArrangeAsync(status: "connected", visibility: "unlisted");
        _client.Inner.ForcedVisibility = YouTubeVisibility.Private;

        var refused = await CheckAsync(project, "public");

        refused.Status.ShouldBe(YouTubeConnectionStatus.Pending);
        var afterRefusal = await StoredAsync(project);
        afterRefusal.Status.ShouldBe("pending");
        afterRefusal.Visibility.ShouldBeNull();
        _client.Inner.Uploads.ShouldHaveSingleItem().Upload.Visibility.ShouldBe(YouTubeVisibility.Public);

        _client.Inner.ForcedVisibility = null;
        var passed = await CheckAsync(project, "public");

        passed.Status.ShouldBe(YouTubeConnectionStatus.Connected);
        passed.Visibility.ShouldBe(YouTubeVisibility.Public);
        var afterPass = await StoredAsync(project);
        afterPass.Status.ShouldBe("connected");
        afterPass.Visibility.ShouldBe("public");
        _client.Inner.Uploads.Count.ShouldBe(2);
        ShouldHaveDeletedEveryUpload();
    }

    [Test]
    public async Task Check_WhenNotAdmin_ShouldBeForbidden()
    {
        var project = await ArrangeAsync();

        using var refused = await CheckRawAsync(project, "unlisted", Api.ActingAs(TestUser.Priya));

        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        _client.Inner.Uploads.ShouldBeEmpty();
        (await StoredAsync(project)).Status.ShouldBe("pending");
    }
}
