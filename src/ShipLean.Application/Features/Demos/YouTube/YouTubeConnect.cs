using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.YouTube;

public sealed class YouTubeConnect : IYouTubeConnect
{
    // TODO: the real connection reads the saved channel; until it does every project reads Off.
    public Task<YouTubeProjectDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new YouTubeProjectDto(YouTubeConnectionStatus.Off, null, null, null, null));

    // TODO: the real start builds Google's consent URL. The stub is a 400, not the 404 of an Off instance, because the access sweep's allowed leg refuses 404.
    public Task<YouTubeStartDto> StartAsync(Guid projectId, string origin, CancellationToken cancellationToken = default)
        => throw ApiException.Validation("YouTube can't be connected yet.");

    // TODO: the real callback validates the state and stores the channel; the stub is the redirect for a state that doesn't open.
    public Task<string> CallbackAsync(Guid? signedInUser, string? code, string? state, string? error, string origin, CancellationToken cancellationToken = default)
        => Task.FromResult("/?youtube=error");

    // TODO: the real disconnect revokes the token first; the stub reports a revoked connection (the sweep's allowed leg refuses 404).
    public Task<YouTubeDisconnectDto> DisconnectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new YouTubeDisconnectDto(true, null));
}
