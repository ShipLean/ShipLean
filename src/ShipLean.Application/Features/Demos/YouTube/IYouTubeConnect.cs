using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

// Connecting a channel: reading its state, starting the consent, the callback, disconnecting. Its own interface so the task that fills it in never edits YouTubeConnections.
public interface IYouTubeConnect
{
    Task<YouTubeProjectDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<YouTubeStartDto> StartAsync(Guid projectId, string origin, CancellationToken cancellationToken = default);

    Task<string> CallbackAsync(Guid? signedInUser, string? code, string? state, string? error, string origin, CancellationToken cancellationToken = default);

    Task<YouTubeDisconnectDto> DisconnectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
