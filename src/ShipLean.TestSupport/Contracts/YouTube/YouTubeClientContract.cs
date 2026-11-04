using ShipLean.Contracts.Demos;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.YouTube;

// Behaviour every IYouTubeClient must have: the fake, and the real client over its local stub server.
// The abstract members build a client and script each mode, so each fixture does that its own way.
[TestFixture]
public abstract class YouTubeClientContract
{
    private static readonly YouTubeClientSettings Settings = new("client-id", "client-secret");
    private static readonly byte[] Video = "not-really-a-video"u8.ToArray();

    protected abstract IYouTubeClient Create();

    // The channel gives every later upload this visibility, whatever the upload chose.
    protected abstract void ForceVisibility(IYouTubeClient client, YouTubeVisibility visibility);

    // Every later upload fails with a Quota failure.
    protected abstract void ExhaustQuota(IYouTubeClient client);

    // Every later token request fails with a Revoked failure.
    protected abstract void RevokeAccess(IYouTubeClient client);

    // The next consent comes back without the youtube.force-ssl scope.
    protected abstract void NarrowGrantedScope(IYouTubeClient client);

    // The next revoke fails.
    protected abstract void FailRevoke(IYouTubeClient client);

    [Test]
    public async Task Upload_ThenRead_ShouldReturnTheChosenPrivacy()
    {
        var client = Create();

        var id = await UploadAsync(client, YouTubeVisibility.Unlisted);

        (await client.ReadPrivacyAsync("token", id, CancellationToken.None)).ShouldBe(YouTubeVisibility.Unlisted);
    }

    [Test]
    public async Task Upload_WhenVisibilityForced_ShouldReadBackWhatTheChannelGave()
    {
        var client = Create();
        ForceVisibility(client, YouTubeVisibility.Private);

        var id = await UploadAsync(client, YouTubeVisibility.Unlisted);

        (await client.ReadPrivacyAsync("token", id, CancellationToken.None)).ShouldBe(YouTubeVisibility.Private);
    }

    [Test]
    public async Task Delete_ShouldRemoveIt()
    {
        var client = Create();
        var id = await UploadAsync(client, YouTubeVisibility.Unlisted);

        await client.DeleteAsync("token", id, CancellationToken.None);

        var read = await Should.ThrowAsync<YouTubeFailureException>(() => client.ReadPrivacyAsync("token", id, CancellationToken.None));
        read.Kind.ShouldBe(YouTubeFailureKind.Rejected);
    }

    [Test]
    public async Task Upload_WhenQuota_ShouldThrowQuotaFailure()
    {
        var client = Create();
        ExhaustQuota(client);

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => UploadAsync(client, YouTubeVisibility.Unlisted));

        failure.Kind.ShouldBe(YouTubeFailureKind.Quota);
        failure.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Token_WhenRevoked_ShouldThrowRevokedFailure()
    {
        var client = Create();
        var tokens = await client.ExchangeCodeAsync(Settings, "code", "https://x.test/cb", CancellationToken.None);
        RevokeAccess(client);

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => client.AccessTokenAsync(Settings, tokens.RefreshToken, CancellationToken.None));

        failure.Kind.ShouldBe(YouTubeFailureKind.Revoked);
        failure.Message.ShouldNotContain(tokens.RefreshToken);
    }

    [Test]
    public async Task Channel_ShouldReturnIdAndTitle()
    {
        var channel = await Create().ChannelAsync("token", CancellationToken.None);

        channel.Id.ShouldNotBeNullOrWhiteSpace();
        channel.Title.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Exchange_ShouldReturnTheGrantedScopes()
    {
        var tokens = await Create().ExchangeCodeAsync(Settings, "code", "https://x.test/cb", CancellationToken.None);

        tokens.GrantedScopes.ShouldContain(FullScope);
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Exchange_WhenNarrowed_ShouldReturnOnlyWhatWasGranted()
    {
        var client = Create();
        NarrowGrantedScope(client);

        var tokens = await client.ExchangeCodeAsync(Settings, "code", "https://x.test/cb", CancellationToken.None);

        tokens.GrantedScopes.ShouldNotContain(FullScope);
        tokens.GrantedScopes.ShouldNotBeEmpty();
    }

    [Test]
    public async Task Revoke_ShouldEndTheRefreshToken()
    {
        var client = Create();
        var tokens = await client.ExchangeCodeAsync(Settings, "code", "https://x.test/cb", CancellationToken.None);
        (await client.AccessTokenAsync(Settings, tokens.RefreshToken, CancellationToken.None)).ShouldNotBeNullOrWhiteSpace();

        await client.RevokeAsync(Settings, tokens.RefreshToken, CancellationToken.None);

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => client.AccessTokenAsync(Settings, tokens.RefreshToken, CancellationToken.None));
        failure.Kind.ShouldBe(YouTubeFailureKind.Revoked);
    }

    [Test]
    public async Task Revoke_WhenItFails_ShouldThrowAYouTubeFailure()
    {
        var client = Create();
        var tokens = await client.ExchangeCodeAsync(Settings, "code", "https://x.test/cb", CancellationToken.None);
        FailRevoke(client);

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => client.RevokeAsync(Settings, tokens.RefreshToken, CancellationToken.None));

        failure.Message.ShouldNotBeNullOrWhiteSpace();
        failure.Message.ShouldNotContain(tokens.RefreshToken);
    }

    private const string FullScope = "https://www.googleapis.com/auth/youtube.force-ssl";

    private static Task<string> UploadAsync(IYouTubeClient client, YouTubeVisibility visibility) =>
        client.UploadAsync(
            "token",
            new YouTubeUpload("Title", "0:00 Start", visibility, "video/mp4"),
            new MemoryStream(Video),
            Video.Length,
            CancellationToken.None);
}
