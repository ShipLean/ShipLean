using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

// Only delegates, one collaborator per method, so the tasks that fill them in never edit this file.
public sealed class YouTubeConnections(IYouTubeConnect connect, IYouTubeCheck check) : IYouTubeConnections
{
    public Task<YouTubeProjectDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
        => connect.ReadAsync(projectId, cancellationToken);

    public Task<YouTubeStartDto> StartAsync(Guid projectId, string origin, CancellationToken cancellationToken = default)
        => connect.StartAsync(projectId, origin, cancellationToken);

    public Task<string> CallbackAsync(Guid? signedInUser, string? code, string? state, string? error, string origin, CancellationToken cancellationToken = default)
        => connect.CallbackAsync(signedInUser, code, state, error, origin, cancellationToken);

    public Task<YouTubeProjectDto> CheckAsync(Guid projectId, YouTubeVisibility visibility, CancellationToken cancellationToken = default)
        => check.CheckAsync(projectId, visibility, cancellationToken);

    public Task<YouTubeDisconnectDto> DisconnectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => connect.DisconnectAsync(projectId, cancellationToken);
}
