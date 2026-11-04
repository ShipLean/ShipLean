namespace ShipLean.Contracts.Demos;

public interface IYouTubeConnections
{
    Task<YouTubeProjectDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<YouTubeStartDto> StartAsync(Guid projectId, string origin, CancellationToken cancellationToken = default);

    Task<string> CallbackAsync(Guid? signedInUser, string? code, string? state, string? error, string origin, CancellationToken cancellationToken = default);

    Task<YouTubeProjectDto> CheckAsync(Guid projectId, YouTubeVisibility visibility, CancellationToken cancellationToken = default);

    Task<YouTubeDisconnectDto> DisconnectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
