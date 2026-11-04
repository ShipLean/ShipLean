using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// A scripted IYouTubeConnections for the endpoint pins: each call returns what the test set, or throws what it set, and records the user the callback was given.
public sealed class FakeYouTubeConnections : IYouTubeConnections
{
    public Exception? Throws { get; set; }

    public YouTubeStartDto Start { get; set; } = new("https://accounts.example.test/consent");

    public string CallbackPath { get; set; } = "/p/SHIP/settings/demos?youtube=pending";

    public YouTubeProjectDto Check { get; set; } = new(YouTubeConnectionStatus.Connected, "Channel", YouTubeVisibility.Unlisted, "Checked just now", null);

    public YouTubeDisconnectDto Disconnect { get; set; } = new(true, null);

    public int Callbacks { get; private set; }

    public Guid? CallbackUser { get; private set; }

    public YouTubeVisibility? CheckedVisibility { get; private set; }

    public Task<YouTubeProjectDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Answer(Check);

    public Task<YouTubeStartDto> StartAsync(Guid projectId, string origin, CancellationToken cancellationToken = default)
        => Answer(Start);

    public Task<string> CallbackAsync(Guid? signedInUser, string? code, string? state, string? error, string origin, CancellationToken cancellationToken = default)
    {
        Callbacks++;
        CallbackUser = signedInUser;
        return Answer(CallbackPath);
    }

    public Task<YouTubeProjectDto> CheckAsync(Guid projectId, YouTubeVisibility visibility, CancellationToken cancellationToken = default)
    {
        CheckedVisibility = visibility;
        return Answer(Check);
    }

    public Task<YouTubeDisconnectDto> DisconnectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Answer(Disconnect);

    public void Reset()
    {
        Throws = null;
        Callbacks = 0;
        CallbackUser = null;
        CallbackPath = "/p/SHIP/settings/demos?youtube=pending";
        Check = new YouTubeProjectDto(YouTubeConnectionStatus.Connected, "Channel", YouTubeVisibility.Unlisted, "Checked just now", null);
    }

    private Task<T> Answer<T>(T value)
        => Throws is { } failure ? Task.FromException<T>(failure) : Task.FromResult(value);
}
