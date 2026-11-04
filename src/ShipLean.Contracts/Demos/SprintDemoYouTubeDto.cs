namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoYouTubeDto(YouTubeDemoState State, string? WatchUrl, string? EmbedUrl, YouTubeVisibility? Privacy, string? FailureMessage);
